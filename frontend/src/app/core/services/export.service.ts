import { Injectable } from '@angular/core';

/** Cómo se muestra una columna: texto, número entero o pesos. */
export type ExportColumnKind = 'text' | 'number' | 'money';

/** Columna de una tabla exportable. */
export interface ExportColumn<T> {
  header: string;
  value: (row: T) => string | number;
  kind?: ExportColumnKind;
}

/** Tabla a exportar: título, línea de filtros, columnas y filas. */
export interface ExportTable<T> {
  /** Nombre del archivo sin extensión; se normaliza (sin tildes ni espacios). */
  fileName: string;
  title: string;
  /** Filtros aplicados, por ejemplo «Mes: septiembre de 2026 · Sede: Bogotá». */
  subtitle: string;
  columns: ExportColumn<T>[];
  rows: T[];
}

const MONEY = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', minimumFractionDigits: 2 });
const INTEGER = new Intl.NumberFormat('es-CO');
const GENERATED = new Intl.DateTimeFormat('es-CO', { timeZone: 'America/Bogota', dateStyle: 'long', timeStyle: 'short' });

/**
 * Normaliza un nombre de archivo: minúsculas, sin tildes y con guiones.
 * @param name Nombre libre, por ejemplo «Transacciones por sede Bogotá».
 * @returns Por ejemplo `transacciones-por-sede-bogota`.
 */
export function safeFileName(name: string): string {
  return name
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

/**
 * Convierte las filas en texto con el formato colombiano (para el PDF).
 * @returns Una lista de celdas por fila, en el orden de las columnas.
 */
export function toTextRows<T>(columns: ExportColumn<T>[], rows: T[]): string[][] {
  return rows.map((row) =>
    columns.map((c) => {
      const value = c.value(row);
      if (typeof value !== 'number') return value;
      return c.kind === 'money' ? MONEY.format(value) : INTEGER.format(value);
    }),
  );
}

/**
 * Descarga tablas en Excel (ExcelJS) o PDF (jsPDF + AutoTable). Las librerías se cargan con
 * `import()` la primera vez que se exporta, así no pesan en la carga inicial de la aplicación.
 */
@Injectable({ providedIn: 'root' })
export class ExportService {
  /** Genera y descarga un .xlsx con título, filtros, encabezado con filtro automático y totales con formato. */
  async toExcel<T>(table: ExportTable<T>): Promise<void> {
    const module = await import('exceljs');
    const excel = ('default' in module ? module.default : module) as typeof import('exceljs');
    const workbook = new excel.Workbook();
    workbook.creator = 'Kadree Bank';
    workbook.created = new Date();
    const sheet = workbook.addWorksheet('Reporte', { views: [{ state: 'frozen', ySplit: 4 }] });
    const width = table.columns.length;

    sheet.addRow([table.title]).font = { bold: true, size: 14 };
    sheet.addRow([table.subtitle]).font = { italic: true, color: { argb: 'FF555555' } };
    sheet.addRow([`Generado: ${GENERATED.format(new Date())}`]).font = { size: 9, color: { argb: 'FF777777' } };
    for (let r = 1; r <= 3; r++) sheet.mergeCells(r, 1, r, width);

    const header = sheet.addRow(table.columns.map((c) => c.header));
    header.eachCell((cell) => {
      cell.font = { bold: true, color: { argb: 'FFFFFFFF' } };
      cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF0B1F3B' } };
      cell.alignment = { vertical: 'middle', wrapText: true };
    });

    for (const row of table.rows) sheet.addRow(table.columns.map((c) => c.value(row)));

    table.columns.forEach((c, i) => {
      const column = sheet.getColumn(i + 1);
      if (c.kind === 'money') column.numFmt = '"$" #,##0.00';
      if (c.kind === 'number') column.numFmt = '#,##0';
      const longest = Math.max(c.header.length, ...table.rows.map((r) => String(c.value(r)).length));
      column.width = Math.min(Math.max(longest + 4, 10), 45);
    });
    sheet.autoFilter = { from: { row: 4, column: 1 }, to: { row: 4, column: width } };

    const buffer = await workbook.xlsx.writeBuffer();
    this.download(
      new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }),
      `${safeFileName(table.fileName)}.xlsx`,
    );
  }

  /** Genera y descarga un PDF horizontal con título, filtros, tabla paginada y numeración de páginas. */
  async toPdf<T>(table: ExportTable<T>): Promise<void> {
    const [{ jsPDF }, { autoTable }] = await Promise.all([import('jspdf'), import('jspdf-autotable')]);
    const doc = new jsPDF({ orientation: 'landscape', unit: 'pt', format: 'a4' });

    doc.setFontSize(16);
    doc.text(table.title, 40, 44);
    doc.setFontSize(10);
    doc.setTextColor(90);
    doc.text(table.subtitle, 40, 62);
    doc.text(`Generado: ${GENERATED.format(new Date())}`, 40, 76);

    autoTable(doc, {
      startY: 90,
      head: [table.columns.map((c) => c.header)],
      body: toTextRows(table.columns, table.rows),
      styles: { fontSize: 9, cellPadding: 5 },
      headStyles: { fillColor: [11, 31, 59], textColor: 255 },
      alternateRowStyles: { fillColor: [244, 246, 250] },
      columnStyles: Object.fromEntries(
        table.columns.map((c, i) => [i, { halign: c.kind && c.kind !== 'text' ? 'right' : 'left' }]),
      ),
      didDrawPage: () => {
        const page = doc.getNumberOfPages();
        doc.setFontSize(8);
        doc.setTextColor(120);
        doc.text(`Kadree Bank · página ${page}`, doc.internal.pageSize.getWidth() - 40, doc.internal.pageSize.getHeight() - 20, {
          align: 'right',
        });
      },
    });

    doc.save(`${safeFileName(table.fileName)}.pdf`);
  }

  private download(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}

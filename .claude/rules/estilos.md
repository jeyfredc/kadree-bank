---
paths:
  - "frontend/src/**/*.css"
  - "frontend/src/**/*.scss"
  - "frontend/src/**/*.html"
  - "frontend/src/index.html"
---

# Reglas de estilos (design system Kadree)

- Antes de crear estilos nuevos, busca la clase que ya existe en `styles/kadree-components.css`
  (componentes `kb-*`) y en `styles.scss` (diseño de página: `kb-page`, `kb-table`, `kb-grid-2`...).
- Colores **siempre** con variables de `styles/kadree-tokens.css` (`var(--text)`, `var(--action)`...).
  Nunca valores fijos como `#1a2b3c`.
- Espacios con los tokens `--space-1/2/3/4/6/8/12/16`. **No existe `--space-5`.**
- Clases nuevas en notación BEM con prefijo `kb-` (`kb-bloque__elemento--modificador`).
- Temas: `data-theme="marino"` (oscuro, por defecto) y `data-theme="claro"`. Revisa que lo nuevo se
  vea bien en los dos.
- Íconos: Material Symbols Rounded. **Si usas un ícono nuevo, agrégalo a `icon_names` en
  `src/index.html`**; si no, se verá el nombre como texto.
- Respeta `prefers-reduced-motion` en cualquier animación.

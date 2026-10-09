import { pageSlots } from './paginator.component';

describe('pageSlots', () => {
  it('muestra todas las páginas cuando son pocas', () => {
    expect(pageSlots(2, 5)).toEqual([1, 2, 3, 4, 5]);
  });

  it('resume con huecos alrededor de la página actual', () => {
    expect(pageSlots(8, 20)).toEqual([1, 'gap', 6, 7, 8, 9, 10, 'gap', 20]);
  });

  it('no agrega hueco junto a los extremos', () => {
    expect(pageSlots(1, 20)).toEqual([1, 2, 3, 'gap', 20]);
    expect(pageSlots(20, 20)).toEqual([1, 'gap', 18, 19, 20]);
  });
});

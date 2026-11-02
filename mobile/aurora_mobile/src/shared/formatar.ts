/**
 * A API manda datas em UTC; algumas vêm sem o "Z" no fim (ex.: 2026-11-02T12:00:00). Sem o sufixo o JavaScript as
 * leria como hora local e erraria o fuso, então o sufixo é acrescentado quando falta.
 */
export function lerDataUtc(texto: string): Date {
  const temFuso = /(Z|[+-]\d{2}:?\d{2})$/.test(texto);
  const apenasData = /^\d{4}-\d{2}-\d{2}$/.test(texto);
  return new Date(apenasData || temFuso ? texto : `${texto}Z`);
}

const comDataEHora = new Intl.DateTimeFormat('pt-BR', {
  day: '2-digit',
  month: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
});
const comHora = new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' });
const moeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const numero = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 });

/** "02/11 09:30" */
export function formatarDataEHora(iso: string): string {
  return comDataEHora.format(lerDataUtc(iso)).replace(',', '');
}

/** "09:30" */
export function formatarHora(iso: string): string {
  return comHora.format(lerDataUtc(iso));
}

/** "13/12/2026" para uma data sem hora (prazo de entrega). */
export function formatarData(data: string): string {
  const [ano, mes, dia] = data.slice(0, 10).split('-');
  return `${dia}/${mes}/${ano}`;
}

export function formatarMoeda(valor: number): string {
  return moeda.format(valor);
}

export function formatarQuantidade(valor: number): string {
  return numero.format(valor);
}

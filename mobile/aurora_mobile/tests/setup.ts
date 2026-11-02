// O SQLite embutido do Node ainda é "experimental" e imprime um aviso a cada execução; ele só roda nos testes.
const emitirAviso = process.emitWarning.bind(process);
process.emitWarning = ((aviso: string | Error, ...resto: unknown[]) => {
  const texto = typeof aviso === 'string' ? aviso : aviso.message;
  if (texto.includes('SQLite is an experimental feature')) return;
  return (emitirAviso as (...args: unknown[]) => void)(aviso, ...resto);
}) as typeof process.emitWarning;

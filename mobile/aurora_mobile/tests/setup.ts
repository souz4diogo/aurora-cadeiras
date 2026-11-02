// O SQLite embutido do Node ainda é "experimental" e imprime um aviso a cada execução; ele só roda nos testes.
const emitirAviso = process.emitWarning.bind(process);
process.emitWarning = ((aviso: string | Error, ...resto: unknown[]) => {
  const texto = typeof aviso === 'string' ? aviso : aviso.message;
  if (texto.includes('SQLite is an experimental feature')) return;
  return (emitirAviso as (...args: unknown[]) => void)(aviso, ...resto);
}) as typeof process.emitWarning;

// Navegação e rede de mentira: as implementações ficam em tests/helpers (a fábrica do mock só faz require).
jest.mock('expo-router', () => require('./helpers/expoRouterFalso'));
jest.mock('@react-native-community/netinfo', () => ({ __esModule: true, ...require('./helpers/netinfoFalso') }));

// Vibração é sensação física: nos testes nada vibra.
jest.mock('@/shared/vibracao', () => ({
  vibrarSucesso: jest.fn(),
  vibrarAlerta: jest.fn(),
  vibrarToque: jest.fn(),
}));

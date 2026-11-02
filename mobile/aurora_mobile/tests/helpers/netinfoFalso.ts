export interface EstadoDaRede {
  isConnected: boolean;
  isInternetReachable: boolean | null;
}

type Ouvinte = (estado: EstadoDaRede) => void;

const ONLINE: EstadoDaRede = { isConnected: true, isInternetReachable: true };
const ouvintes = new Set<Ouvinte>();
let estadoAtual: EstadoDaRede = ONLINE;

/** NetInfo de mentira: lembra o último estado (quem assina depois recebe o estado vigente, como no real). */
export default {
  addEventListener(ouvinte: Ouvinte) {
    ouvintes.add(ouvinte);
    ouvinte(estadoAtual);
    return () => {
      ouvintes.delete(ouvinte);
    };
  },
};

export function __emitir(estado: EstadoDaRede): void {
  estadoAtual = estado;
  ouvintes.forEach((ouvinte) => ouvinte(estado));
}

/** Cada teste começa online. */
export function __redefinir(): void {
  estadoAtual = ONLINE;
  ouvintes.clear();
}

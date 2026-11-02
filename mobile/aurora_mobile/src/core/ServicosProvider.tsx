import { createContext, useContext, type ReactNode } from 'react';

import type { Servicos } from '@/core/servicos';

const Contexto = createContext<Servicos | null>(null);

/** Entrega os serviços às telas. Nos testes entra um conjunto de serviços falsos no lugar dos reais. */
export function ServicosProvider({ servicos, children }: { servicos: Servicos; children: ReactNode }) {
  return <Contexto.Provider value={servicos}>{children}</Contexto.Provider>;
}

export function useServicos(): Servicos {
  const servicos = useContext(Contexto);
  if (!servicos) throw new Error('useServicos precisa estar dentro de ServicosProvider.');
  return servicos;
}

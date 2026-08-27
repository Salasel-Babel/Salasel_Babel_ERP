/* سياق النقل: نسخة واحدة من الاعتماد والعنوان، ومنها يبني كل نداء نفسه. */
import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";
import { fetchTransport, type Transport } from "../api/transport";
import { loadConfig, saveConfig, type ApiConfig } from "./config";

interface ApiContextValue {
  transport: Transport;
  config: ApiConfig;
  setConfig: (next: ApiConfig) => void;
}

const ApiContext = createContext<ApiContextValue | null>(null);

/**
 * يوفّر النقل والإعداد.
 * @param props الأبناء، ونقل بديل للاختبارات.
 */
export function ApiProvider(props: { children: ReactNode; transport?: Transport }): ReactNode {
  const [config, setConfigState] = useState<ApiConfig>(() =>
    loadConfig(globalThis.location?.search ?? "")
  );

  const setConfig = useCallback((next: ApiConfig) => {
    saveConfig(next);
    setConfigState(next);
  }, []);

  const transport = useMemo<Transport>(
    () =>
      props.transport ??
      fetchTransport({ baseUrl: config.baseUrl, ...(config.token ? { token: config.token } : {}) }),
    [config.baseUrl, config.token, props.transport]
  );

  const value = useMemo(() => ({ transport, config, setConfig }), [transport, config, setConfig]);
  return <ApiContext.Provider value={value}>{props.children}</ApiContext.Provider>;
}

/** النقل والإعداد. */
export function useApi(): ApiContextValue {
  const value = useContext(ApiContext);
  if (!value) throw new Error("useApi: خارج ApiProvider. / outside ApiProvider.");
  return value;
}

/* ═══════════════════════════════════════════════════════════════════════════
   قوائمُ الاختيار  ·  Pickers
   ───────────────────────────────────────────────────────────────────────────
   معرّفُ العميل والمورد والموظف والصنف كان يُكتب بالنصّ في كل مستند. وهنا يُختار
   من قائمة الخادم، والمعرّف الذي يعبر إلى السلك هو ما يعلنه العقد: `Party.id`
   و`HrEmployee.id` و`Item.code`. وإن لم تصل القائمة (خطأ، أو سجلٌّ فارغ) يبقى
   حقلُ النصّ كما كان — فلا يُغلق بابٌ كان مفتوحاً.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { listCustomers, listEmployees, listItems, listSuppliers } from "../api/generated/client";
import type { Item } from "../api/generated/types";
import { useT } from "../i18n/react";
import { useApi } from "./api-context";

interface Option {
  readonly value: string;
  readonly label: string;
}

function Picker(props: {
  readonly id: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly options: readonly Option[] | undefined;
  readonly testId: string;
}): ReactNode {
  const { t } = useT();
  if (!props.options || props.options.length === 0) {
    return (
      <input
        id={props.id}
        className="ctl mono"
        dir="ltr"
        autoComplete="off"
        spellCheck={false}
        data-testid={props.testId}
        value={props.value}
        onChange={(e) => props.onChange(e.target.value)}
      />
    );
  }
  const known = props.options.some((option) => option.value === props.value);
  return (
    <select id={props.id} className="ctl" data-testid={props.testId} value={props.value} onChange={(e) => props.onChange(e.target.value)}>
      <option value="">{t("app.picker.choose")}</option>
      {!known && props.value !== "" ? <option value={props.value}>{props.value}</option> : null}
      {props.options.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  );
}

/** عميلٌ أو مورد من سجلّه. القيمة `Party.id`. */
export function PartyPicker(props: {
  readonly kind: "customer" | "supplier";
  readonly id: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly testId: string;
}): ReactNode {
  const { transport, config } = useApi();
  const parties = useQuery({
    queryKey: ["pickers", props.kind, config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    staleTime: 60_000,
    queryFn: ({ signal }) =>
      props.kind === "customer"
        ? listCustomers(transport, { companyId: config.companyId }, signal)
        : listSuppliers(transport, { companyId: config.companyId }, signal),
  });
  const options = parties.data?.parties.map((party) => ({ value: party.id, label: party.code + " — " + party.name.ar }));
  return <Picker id={props.id} value={props.value} onChange={props.onChange} options={options} testId={props.testId} />;
}

/** موظفٌ من السجلّ. القيمة `HrEmployee.id`. */
export function EmployeePicker(props: {
  readonly id: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly testId: string;
}): ReactNode {
  const { transport, config } = useApi();
  const employees = useQuery({
    queryKey: ["pickers", "employees", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    staleTime: 60_000,
    queryFn: ({ signal }) => listEmployees(transport, { companyId: config.companyId }, signal),
  });
  const options = employees.data?.items.map((employee) => ({ value: employee.id, label: employee.code + " — " + employee.nameAr }));
  return <Picker id={props.id} value={props.value} onChange={props.onChange} options={options} testId={props.testId} />;
}

/** الأصناف كما يعيدها الخادم — للشاشات التي تشتقّ الوصف والمجموعة من الصنف. */
export function useItems(): readonly Item[] | undefined {
  const { transport, config } = useApi();
  const items = useQuery({
    queryKey: ["pickers", "items", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    staleTime: 60_000,
    queryFn: ({ signal }) => listItems(transport, { companyId: config.companyId }, signal),
  });
  return items.data?.items;
}

/** صنفٌ من السجلّ. القيمة `Item.code`، ويعود الصنف كاملاً ليشتقّ منه الوصف والمجموعة. */
export function ItemPicker(props: {
  readonly id: string;
  readonly value: string;
  readonly onChange: (item: Item | null, code: string) => void;
  readonly testId: string;
}): ReactNode {
  const items = useItems();
  const options = items?.map((item) => ({ value: item.code, label: item.code + " — " + item.name.ar }));
  return (
    <Picker
      id={props.id}
      value={props.value}
      onChange={(code) => props.onChange(items?.find((item) => item.code === code) ?? null, code)}
      options={options}
      testId={props.testId}
    />
  );
}

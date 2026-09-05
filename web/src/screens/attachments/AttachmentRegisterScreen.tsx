/* ═══════════════════════════════════════════════════════════════════════════
   سجلّ المرفقات — ما أُودع على هذا المستند، وإيداعُ سندٍ واستخراجُ نسخة
   The attachment register — what was filed against a document, filing, pulling
   ───────────────────────────────────────────────────────────────────────────
   السؤال الذي تجيبه هذه الشاشة واحد: **«ما السندات المودعة على هذا المستند،
   ومن أودعها ومتى، وكيف أُودع سنداً وأُخرج نسخةً منه؟»** ولا تجيب سؤال
   «هل هذا السند هو الإصدار القائم وماذا أفعل به؟» — ذاك سؤالٌ ثانٍ في شاشةٍ
   ثانية، والقسمة مبرَّرةٌ في `ADR-0082-attachments-split-by-hand-items-do-not`.

   خمسة قرارات تحكمها، وكلّها مقروءة من العقد لا مفترَضة:

   ١ · **الترشيح بحقلين معاً أو بلا حقل.** العقد يرفض نصف الربط بـ400 ورمزٍ
       ثابت `storage.source_document_incomplete`. فالشاشة **تقول الرفض قبل
       الضغط** وتُعطّل الطلب، ولا ترسل استعلاماً تعرف أنه مرفوض.

   ٢ · **الجرد وصفٌ لا محتوى — ولا بايتة تعبر منه.** فالاستخراج لوحٌ مستقلّ،
       ولا زرَّ «فتح» على صفٍّ يُوهم أن الملفّ في القائمة.

   ٣ · **الحدود تُقال قبل الضغط لا بعده**: عشرون ميبيبايت سقفاً (413)،
       وستّة أنواع مقبولة يُشمّها الخادم من البايتات، وحجم صفحةٍ سقفه مئة
       **يُرفض ولا يُقصّ** (`storage.page_refused`).

   ٤ · **المسحوب يبقى في الجرد بحالته.** السحب علامةٌ لا حذف — لا بايتة
       تُمحى ولا بصمة — فإخفاؤه خلف مرشّحٍ افتراضي كان سيجعله يُظنّ محذوفاً،
       وهو باقٍ في القاعدة إلى الأبد. والحكم نفسه المعمول به في سجلّ التسكين.

   ٥ · **النوع المُعلَن ليس حكماً، والشاشة لا تدّعي أنه كذلك.** الخادم يشمّ
       البايتات، فإعلانٌ خارج الستّة **تحذيرٌ** لا رفضٌ مسبق: ملفٌّ بلا امتداد
       قد تكون بايتاته PNG سليمة. وما يُرفض مسبقاً هو ما يعرفه المتصفّح يقيناً:
       الحجم، والفراغ، ونصف الربط، ورمزٌ ليس رمزاً.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { depositAttachment, listAttachments } from "../../api/generated/client";
import type { Attachment } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Num, useT } from "../../i18n/react";
import { Button, EmptyState, Field, MOTION, Panel, StatCard, useMoment } from "../../ui";
import {
  ACCEPTED_MEDIA_TYPES,
  AttachmentFlags,
  ChooseCompanyFirst,
  DownloadTicketPanel,
  MAX_ATTACHMENT_BYTES,
  MAX_PAGE_TAKE,
  PAGE_NUMBER_RE,
  ReadingSkeleton,
  RefusalNextStep,
  SOURCE_TYPE_RE,
  SurfaceGap,
} from "./shared";

/** شكل المعرّف الكوني كما ينشره العقد لحقول `format: uuid`. */
const UUID_RE = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

/* ══════════════════════════════════════════════════════ الشاشة كاملةً */

/** شاشة سجلّ المرفقات: جردٌ وإيداعٌ واستخراج. */
export function AttachmentRegisterScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [arrived, fireArrive] = useMoment("arrive");

  /* ── الترشيح والتصفيح ─────────────────────────────────────────────── */
  const [filterType, setFilterType] = useState("");
  const [filterId, setFilterId] = useState("");
  const [skip, setSkip] = useState("0");
  const [take, setTake] = useState("50");

  /* ── الإيداع ──────────────────────────────────────────────────────── */
  const [file, setFile] = useState<File | null>(null);
  const [sourceType, setSourceType] = useState("");
  const [sourceId, setSourceId] = useState("");
  const [deposited, setDeposited] = useState<Attachment | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  /* ── الاستخراج ────────────────────────────────────────────────────── */
  const [picked, setPicked] = useState<string | null>(null);

  /* ═══════ الرفض قبل الضغط: الترشيح ═══════════════════════════════════ */
  const filterHalfLink = (filterType.trim() === "") !== (filterId.trim() === "");
  const filterTypeBad = filterType.trim() !== "" && !SOURCE_TYPE_RE.test(filterType.trim());
  const filterIdBad = filterId.trim() !== "" && !UUID_RE.test(filterId.trim());
  const pageBad =
    !PAGE_NUMBER_RE.test(skip) ||
    !PAGE_NUMBER_RE.test(take) ||
    take === "0" ||
    /* حجم الصفحة سقفه مئة، والمقارنة على الطول ثمّ على القيمة النصّية:
       ثلاثة محارف فأقلّ دون المئة إلا «100» نفسها، وما زاد طولُه أكبر. */
    take.replace(/^0+/, "").length > 3 ||
    (take.replace(/^0+/, "").length === 3 && take.replace(/^0+/, "") !== String(MAX_PAGE_TAKE));
  const filterUsable = !filterHalfLink && !filterTypeBad && !filterIdBad && !pageBad;

  const result = useQuery({
    queryKey: [
      "attachments",
      config.baseUrl,
      config.token,
      config.companyId,
      filterType,
      filterId,
      skip,
      take,
    ],
    enabled: config.companyId !== "" && filterUsable,
    retry: false,
    queryFn: ({ signal }) =>
      listAttachments(
        transport,
        {
          companyId: config.companyId,
          skip,
          take,
          ...(filterType.trim() === ""
            ? {}
            : { sourceDocumentType: filterType.trim(), sourceDocumentId: filterId.trim() }),
        },
        signal
      ),
  });

  const rows: readonly Attachment[] = useMemo(() => result.data?.items ?? [], [result.data]);
  const chosen = useMemo(() => rows.find((row) => row.id === picked) ?? null, [rows, picked]);

  const withdrawnCount = useMemo(
    () => rows.filter((row) => row.withdrawal !== null).length,
    [rows]
  );

  /* ═══════ الرفض قبل الضغط: الإيداع ══════════════════════════════════ */
  const depositHalfLink = (sourceType.trim() === "") !== (sourceId.trim() === "");
  const depositTypeBad = sourceType.trim() !== "" && !SOURCE_TYPE_RE.test(sourceType.trim());
  const depositIdBad = sourceId.trim() !== "" && !UUID_RE.test(sourceId.trim());
  const tooLarge = file !== null && file.size > MAX_ATTACHMENT_BYTES;
  const emptyFile = file !== null && file.size === 0;
  /* النوع المُعلَن **تحذيرٌ لا رفض**: الحكم للبايتات لا للترويسة. */
  const declaredOutside =
    file !== null && file.type !== "" && !ACCEPTED_MEDIA_TYPES.includes(file.type);

  const readyToDeposit =
    file !== null &&
    !tooLarge &&
    !emptyFile &&
    !depositHalfLink &&
    !depositTypeBad &&
    !depositIdBad;

  const submit = useCallback(async () => {
    if (!file) return;
    setBusy(true);
    setError(null);
    try {
      /* حمولة `multipart/form-data` كما ينصّ العقد — والنقل يمرّرها كما هي
         بلا ترويسة نوع محتوى، فالحدّ الفاصل يكتبه المتصفّح. ولا ترميز نصّي
         للبايتات: جسم JSON كان سينفخها الثلث ويضعها في سجلّ الطلب. */
      const form = new FormData();
      form.append("content", file, file.name);
      if (sourceType.trim() !== "") {
        form.append("sourceDocumentType", sourceType.trim());
        form.append("sourceDocumentId", sourceId.trim());
      }
      const stored = await depositAttachment(transport, {
        companyId: config.companyId,
        body: form,
      });
      setDeposited(stored);
      setPicked(stored.id);
      fireArrive();
      await result.refetch();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [config.companyId, file, fireArrive, result, sourceId, sourceType, transport]);

  if (config.companyId === "") return <ChooseCompanyFirst />;

  return (
    <section className="stack" data-testid="attachment-register-screen">
      <header className="pagehead">
        <div>
          <h1>{t("accounting.attach.reg.title")}</h1>
          <p className="sub">{t("accounting.attach.reg.lede")}</p>
        </div>
      </header>

      {result.data ? (
        <div className="stats-row" data-testid="attach-stats">
          <StatCard
            label={t("accounting.attach.reg.statTotal")}
            count={result.data.total}
            hint={t("accounting.attach.reg.statTotalHint")}
            moment={arrived}
            testId="stat-attach-total"
          />
          <StatCard
            label={t("accounting.attach.reg.statPage")}
            count={rows.length}
            hint={t("accounting.attach.reg.statPageHint")}
            testId="stat-attach-page"
          />
          <StatCard
            label={t("accounting.attach.reg.statWithdrawn")}
            count={withdrawnCount}
            tone={withdrawnCount > 0 ? "bad" : "good"}
            hint={t("accounting.attach.reg.statWithdrawnHint")}
            testId="stat-attach-withdrawn"
          />
        </div>
      ) : null}

      <div className="filterbar" role="search">
        <Field
          id="attach-f-type"
          label={t("accounting.attach.reg.filterType")}
          hint={t("accounting.attach.reg.filterTypeHint")}
          error={filterTypeBad ? t("accounting.attach.reg.badTypeCode") : undefined}
        >
          <input
            id="attach-f-type"
            className={"ctl mono" + (filterTypeBad ? " is-invalid" : "")}
            dir="ltr"
            autoComplete="off"
            aria-invalid={filterTypeBad}
            data-testid="attach-filter-type"
            value={filterType}
            onChange={(e) => setFilterType(e.target.value)}
          />
        </Field>
        <Field
          id="attach-f-id"
          label={t("accounting.attach.reg.filterId")}
          hint={t("accounting.attach.reg.filterIdHint")}
          error={filterIdBad ? t("accounting.attach.reg.badUuid") : undefined}
        >
          <input
            id="attach-f-id"
            className={"ctl mono" + (filterIdBad ? " is-invalid" : "")}
            dir="ltr"
            autoComplete="off"
            aria-invalid={filterIdBad}
            data-testid="attach-filter-id"
            value={filterId}
            onChange={(e) => setFilterId(e.target.value)}
          />
        </Field>
        <Field
          id="attach-f-skip"
          label={t("accounting.attach.reg.skip")}
          hint={t("accounting.attach.reg.skipHint")}
        >
          <input
            id="attach-f-skip"
            className="ctl mono"
            dir="ltr"
            inputMode="numeric"
            autoComplete="off"
            data-testid="attach-skip"
            value={skip}
            onChange={(e) => setSkip(e.target.value)}
          />
        </Field>
        <Field
          id="attach-f-take"
          label={t("accounting.attach.reg.take")}
          hint={t("accounting.attach.reg.takeHint")}
          error={pageBad ? t("accounting.attach.reg.badPage") : undefined}
        >
          <input
            id="attach-f-take"
            className={"ctl mono" + (pageBad ? " is-invalid" : "")}
            dir="ltr"
            inputMode="numeric"
            autoComplete="off"
            aria-invalid={pageBad}
            data-testid="attach-take"
            value={take}
            onChange={(e) => setTake(e.target.value)}
          />
        </Field>
        <div className="rowctl">
          <div className="inline-group">
            <Button
              label={t("common.action.refresh")}
              disabled={!filterUsable}
              onClick={() => void result.refetch()}
              testId="attach-reload"
            />
          </div>
        </div>
      </div>

      {filterHalfLink ? (
        <p
          className="alert alert--warning cine-refuse"
          role="status"
          data-testid="attach-half-link"
          data-code="storage.source_document_incomplete"
        >
          {t("accounting.attach.reg.halfLink")}
        </p>
      ) : null}

      {result.isPending && result.fetchStatus === "fetching" ? <ReadingSkeleton /> : null}
      {result.isError ? (
        <>
          <ProblemPanel error={result.error} onRetry={() => void result.refetch()} />
          <RefusalNextStep error={result.error} />
        </>
      ) : null}

      {result.data && rows.length === 0 ? (
        <EmptyState
          title={t("accounting.attach.reg.emptyTitle")}
          body={t("accounting.attach.reg.emptyBody")}
          testId="attach-empty"
        />
      ) : null}

      {rows.length > 0 ? (
        <Panel
          title={t("accounting.attach.reg.tableTitle")}
          note={t("accounting.attach.reg.tableNote")}
          testId="attach-table-panel"
        >
          <div className="ledger" data-state="ready" data-testid="attach-table">
            <table>
              <caption className="visually-hidden">{t("accounting.attach.reg.tableTitle")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("accounting.attach.reg.colFile")}</th>
                  <th scope="col">{t("accounting.attach.reg.colType")}</th>
                  <th scope="col" className="n">{t("accounting.attach.reg.colBytes")}</th>
                  <th scope="col" className="n">{t("accounting.attach.reg.colVersion")}</th>
                  <th scope="col">{t("accounting.attach.reg.colStored")}</th>
                  <th scope="col">{t("accounting.attach.reg.colSource")}</th>
                  <th scope="col">{t("accounting.attach.reg.colState")}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr
                    key={row.id}
                    data-testid="attach-row"
                    data-selected={row.id === picked ? "true" : undefined}
                    data-active={row.withdrawal === null ? "true" : "false"}
                    className={deposited && deposited.id === row.id ? MOTION.arrive : undefined}
                  >
                    <td>
                      <button
                        type="button"
                        className="btn btn-ghost btn-sm"
                        data-testid="attach-pick"
                        aria-pressed={row.id === picked}
                        onClick={() => setPicked(row.id)}
                      >
                        {row.fileName}
                      </button>
                    </td>
                    <td className="code">{row.mediaType}</td>
                    <td className="n"><Num value={row.byteLength} /></td>
                    <td className="n"><Num value={row.version} /></td>
                    <td>
                      <span className="mono" dir="ltr">{row.storedAt}</span>
                      <span className="alt mono" dir="ltr">{row.storedBy}</span>
                    </td>
                    <td>
                      {row.sourceDocumentType === null ? (
                        <span className="muted">{t("accounting.attach.reg.noSource")}</span>
                      ) : (
                        <>
                          <span className="code">{row.sourceDocumentType}</span>
                          <span className="alt mono" dir="ltr">{row.sourceDocumentId}</span>
                        </>
                      )}
                    </td>
                    <td>
                      <AttachmentFlags attachment={row} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="muted">{t("accounting.attach.reg.withdrawnStays")}</p>
        </Panel>
      ) : null}

      {chosen ? <DownloadTicketPanel attachment={chosen} /> : null}

      {deposited ? (
        <section
          className={"alert alert--success " + arrived}
          role="status"
          data-testid="attach-deposited"
        >
          <p>{t("accounting.attach.reg.deposited")}</p>
          <div className="kv">
            <div>
              <div className="k">{t("accounting.attach.reg.colFile")}</div>
              <div className="v">{deposited.fileName}</div>
            </div>
            <div>
              <div className="k">{t("accounting.attach.reg.sniffed")}</div>
              <div className="v code">{deposited.mediaType}</div>
            </div>
            <div>
              <div className="k">{t("accounting.attach.reg.digest")}</div>
              <div className="v mono" dir="ltr">{deposited.contentHash}</div>
            </div>
            <div>
              <div className="k">{t("accounting.attach.reg.colBytes")}</div>
              <div className="v n"><Num value={deposited.byteLength} /></div>
            </div>
          </div>
        </section>
      ) : null}

      <Panel
        title={t("accounting.attach.reg.deposit")}
        note={t("accounting.attach.reg.depositNote")}
        testId="attach-deposit-form"
      >
        <div className="grid fields-3">
          <Field
            id="attach-file"
            label={t("accounting.attach.reg.file")}
            hint={t("accounting.attach.reg.fileHint")}
            error={
              tooLarge
                ? t("accounting.attach.reg.tooLarge")
                : emptyFile
                  ? t("accounting.attach.reg.emptyFile")
                  : undefined
            }
            required
          >
            <input
              id="attach-file"
              className="ctl"
              type="file"
              accept={ACCEPTED_MEDIA_TYPES.join(",")}
              aria-invalid={tooLarge || emptyFile}
              data-testid="attach-file"
              onChange={(e) => {
                setFile(e.target.files?.item(0) ?? null);
                setDeposited(null);
                setError(null);
              }}
            />
          </Field>
          <Field
            id="attach-src-type"
            label={t("accounting.attach.reg.sourceType")}
            hint={t("accounting.attach.reg.sourceTypeHint")}
            error={
              depositTypeBad
                ? t("accounting.attach.reg.badTypeCode")
                : depositHalfLink && sourceType.trim() === ""
                  ? t("accounting.attach.reg.needType")
                  : undefined
            }
          >
            <input
              id="attach-src-type"
              className={"ctl mono" + (depositTypeBad ? " is-invalid" : "")}
              dir="ltr"
              autoComplete="off"
              aria-invalid={depositTypeBad}
              data-testid="attach-source-type"
              value={sourceType}
              onChange={(e) => setSourceType(e.target.value)}
            />
          </Field>
          <Field
            id="attach-src-id"
            label={t("accounting.attach.reg.sourceId")}
            hint={t("accounting.attach.reg.sourceIdHint")}
            error={
              depositIdBad
                ? t("accounting.attach.reg.badUuid")
                : depositHalfLink && sourceId.trim() === ""
                  ? t("accounting.attach.reg.needId")
                  : undefined
            }
          >
            <input
              id="attach-src-id"
              className={"ctl mono" + (depositIdBad ? " is-invalid" : "")}
              dir="ltr"
              autoComplete="off"
              aria-invalid={depositIdBad}
              data-testid="attach-source-id"
              value={sourceId}
              onChange={(e) => setSourceId(e.target.value)}
            />
          </Field>
        </div>

        {declaredOutside ? (
          <p className="alert alert--warning" role="status" data-testid="attach-declared-warn">
            {t("accounting.attach.reg.declaredOutside")}
          </p>
        ) : null}

        <p className="muted" data-testid="attach-limits">{t("accounting.attach.reg.limits")}</p>

        <div className="inline-group">
          <Button
            label={busy ? t("common.state.loading") : t("accounting.attach.reg.submit")}
            kind="primary"
            disabled={!readyToDeposit || busy}
            loading={busy}
            onClick={() => void submit()}
            testId="attach-submit"
          />
        </div>
      </Panel>

      {error ? (
        <>
          <ProblemPanel error={error} />
          <RefusalNextStep error={error} />
        </>
      ) : null}

      <SurfaceGap
        title={t("accounting.attach.reg.gapTitle")}
        body={t("accounting.attach.reg.gapBody")}
        owed={t("accounting.attach.reg.gapOwed")}
        testId="attach-gap-no-doc-names"
      />
    </section>
  );
}

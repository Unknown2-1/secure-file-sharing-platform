import { translate } from "@/lib/i18n/messages";

type StateCopy = {
  title?: string;
  description?: string;
};

type EmptyStateProps = StateCopy & {
  actionLabel?: string;
  onAction?: () => void;
};

type ErrorStateProps = StateCopy & {
  correlationId?: string;
  onRetry?: () => void;
};

export function LoadingState({
  title = translate("id", "state.loading.title"),
  description = translate("id", "state.loading.description"),
}: StateCopy) {
  return (
    <section className="state-panel" role="status" aria-live="polite" aria-busy="true">
      <span className="state-indicator" aria-hidden="true" />
      <StateCopy title={title} description={description} />
    </section>
  );
}

export function EmptyState({
  title = translate("id", "state.empty.title"),
  description = translate("id", "state.empty.description"),
  actionLabel,
  onAction,
}: EmptyStateProps) {
  return (
    <section className="state-panel">
      <StateCopy title={title} description={description} />
      {actionLabel && onAction && <button className="button-secondary mt-4" type="button" onClick={onAction}>{actionLabel}</button>}
    </section>
  );
}

export function ErrorState({
  title = translate("id", "state.error.title"),
  description = translate("id", "state.error.description"),
  correlationId,
  onRetry,
}: ErrorStateProps) {
  return (
    <section className="state-panel border-red-200 bg-red-50" role="alert">
      <StateCopy title={title} description={description} />
      {correlationId && <p className="mt-3 break-all font-mono text-xs text-slate-700">Correlation ID: {correlationId}</p>}
      {onRetry && <button className="button-secondary mt-4" type="button" onClick={onRetry}>{translate("id", "common.retry")}</button>}
    </section>
  );
}

function StateCopy({ title, description }: Required<StateCopy>) {
  return <div><h2 className="font-bold text-slate-900">{title}</h2><p className="mt-1 text-sm leading-6 text-slate-600">{description}</p></div>;
}

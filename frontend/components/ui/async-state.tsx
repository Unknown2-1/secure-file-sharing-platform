"use client";

import { useLocale } from "@/lib/i18n/locale-context";
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
  title,
  description,
}: StateCopy) {
  const { locale } = useLocale();
  const resolvedTitle = title ?? translate(locale, "state.loading.title");
  const resolvedDesc = description ?? translate(locale, "state.loading.description");

  return (
    <section
      className="state-panel"
      role="status"
      aria-live="polite"
      aria-busy="true"
    >
      <span className="state-indicator" aria-hidden="true" />
      <div className="state-content">
        <h2 className="state-title">{resolvedTitle}</h2>
        <p className="state-description">{resolvedDesc}</p>
      </div>
    </section>
  );
}

export function EmptyState({
  title,
  description,
  actionLabel,
  onAction,
}: EmptyStateProps) {
  const { locale } = useLocale();
  const resolvedTitle = title ?? translate(locale, "state.empty.title");
  const resolvedDesc = description ?? translate(locale, "state.empty.description");

  return (
    <section className="state-panel">
      <div className="state-content">
        <h2 className="state-title">{resolvedTitle}</h2>
        <p className="state-description">{resolvedDesc}</p>
      </div>
      {actionLabel && onAction && (
        <button
          className="button-secondary mt-4"
          type="button"
          onClick={onAction}
        >
          {actionLabel}
        </button>
      )}
    </section>
  );
}

export function ErrorState({
  title,
  description,
  correlationId,
  onRetry,
}: ErrorStateProps) {
  const { locale } = useLocale();
  const resolvedTitle = title ?? translate(locale, "state.error.title");
  const resolvedDesc = description ?? translate(locale, "state.error.description");

  return (
    <section className="state-panel border-red-200 bg-red-50" role="alert">
      <div className="state-content">
        <h2 className="state-title">{resolvedTitle}</h2>
        <p className="state-description">{resolvedDesc}</p>
      </div>
      {correlationId && (
        <p className="mt-3 break-all font-mono text-xs text-slate-700">
          Correlation ID: {correlationId}
        </p>
      )}
      {onRetry && (
        <button
          className="button-secondary mt-4"
          type="button"
          onClick={onRetry}
        >
          {translate(locale, "common.retry")}
        </button>
      )}
    </section>
  );
}

import { ApiError } from '../api/client'

export type FieldErrors = Record<string, string[]>

/** Splits a failed submit into messages per field, or one message for the whole form. */
export function toFormErrors(error: unknown, fallback: string): { fieldErrors: FieldErrors; formError: string | null } {
  if (!(error instanceof ApiError)) return { fieldErrors: {}, formError: fallback }
  const fieldErrors = error.fieldErrors
  return Object.keys(fieldErrors).length > 0 ? { fieldErrors, formError: null } : { fieldErrors: {}, formError: error.message }
}

const errorId = (formId: string, field: string) => `${formId}-${field}-error`

/** Marks an input invalid and links it to its message, so screen readers read them together. */
export function errorAttributes(formId: string, field: string, errors: FieldErrors) {
  return errors[field] ? { 'aria-invalid': true, 'aria-describedby': errorId(formId, field) } : {}
}

export function FieldError({ formId, field, errors }: { formId: string; field: string; errors: FieldErrors }) {
  const messages = errors[field]
  return messages ? (
    <p className="field-error" id={errorId(formId, field)}>
      {messages.join(' ')}
    </p>
  ) : null
}

export function FormError({ message }: { message: string | null }) {
  return message ? (
    <p className="form-error" role="alert">
      {message}
    </p>
  ) : null
}

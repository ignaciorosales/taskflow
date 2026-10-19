import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'

// jsdom has <dialog> but not its methods; browsers do. Just enough to open and close one.
const dialog = (globalThis.HTMLDialogElement ?? HTMLElement).prototype as HTMLDialogElement
dialog.showModal ??= function (this: HTMLDialogElement) {
  this.setAttribute('open', '')
}
dialog.close ??= function (this: HTMLDialogElement) {
  this.removeAttribute('open')
  this.dispatchEvent(new Event('close'))
}

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

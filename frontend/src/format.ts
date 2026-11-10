const BANGLA_ZERO = 0x09e6

/** Converts Bangla digits (০-৯) to ASCII digits; everything else is left alone. */
export function toAsciiDigits(text: string): string {
  return text.replace(/[০-৯]/g, (digit) => String(digit.charCodeAt(0) - BANGLA_ZERO))
}

export function toBanglaDigits(text: string | number): string {
  return String(text).replace(/[0-9]/g, (digit) => String.fromCharCode(BANGLA_ZERO + Number(digit)))
}

/** 125 -> "০২:০৫" */
export function formatCountdown(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return toBanglaDigits(`${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`)
}

export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('bn-BD', { dateStyle: 'medium', timeStyle: 'medium' })
}

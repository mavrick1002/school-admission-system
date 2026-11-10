import { createContext, useContext, type RefObject } from 'react'
import { toAsciiDigits } from '../format'

const STORAGE_KEY = 'admission.studentId'
const PATTERN = /^[0-9]{17}$/

/** Bangla digits become ASCII; spaces and dashes people type for readability are dropped. */
export function cleanStudentId(raw: string): string {
  return toAsciiDigits(raw).replace(/[\s-]/g, '')
}

export function isValidStudentId(value: string): boolean {
  return PATTERN.test(value)
}

// Storage can be missing or throw (private mode, blocked site data); the app works without it.
export function loadStudentId(): string {
  try {
    return localStorage.getItem(STORAGE_KEY) ?? ''
  } catch {
    return ''
  }
}

export function saveStudentId(value: string) {
  try {
    if (value) localStorage.setItem(STORAGE_KEY, value)
    else localStorage.removeItem(STORAGE_KEY)
  } catch {
    // Not persisted; it still works for this visit.
  }
}

export interface StudentIdState {
  /** What is in the box, after cleaning. */
  input: string
  setInput: (raw: string) => void
  /** The ID when it is exactly 17 digits, otherwise null. */
  validId: string | null
  inputRef: RefObject<HTMLInputElement | null>
  focusInput: () => void
}

export const StudentIdContext = createContext<StudentIdState | null>(null)

export function useStudentId(): StudentIdState {
  const state = useContext(StudentIdContext)
  if (!state) throw new Error('useStudentId must be used inside StudentIdProvider')
  return state
}

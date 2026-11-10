import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react'
import {
  StudentIdContext,
  cleanStudentId,
  isValidStudentId,
  loadStudentId,
  saveStudentId,
  type StudentIdState,
} from './studentId'

export function StudentIdProvider({ children }: { children: ReactNode }) {
  const [input, setInputState] = useState(() => cleanStudentId(loadStudentId()))
  const inputRef = useRef<HTMLInputElement | null>(null)

  const setInput = useCallback((raw: string) => {
    const cleaned = cleanStudentId(raw)
    setInputState(cleaned)
    saveStudentId(cleaned)
  }, [])

  const focusInput = useCallback(() => {
    inputRef.current?.focus()
    inputRef.current?.scrollIntoView({ block: 'center', behavior: 'smooth' })
  }, [])

  const value = useMemo<StudentIdState>(
    () => ({
      input,
      setInput,
      validId: isValidStudentId(input) ? input : null,
      inputRef,
      focusInput,
    }),
    [input, setInput, focusInput],
  )

  return <StudentIdContext.Provider value={value}>{children}</StudentIdContext.Provider>
}

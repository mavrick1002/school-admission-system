import { toBanglaDigits } from '../format'
import { useStudentId } from '../student/studentId'

const LENGTH = 17

function hint(input: string, valid: boolean): { text: string; tone: 'ok' | 'info' | 'error' } {
  if (valid) return { text: '✓ নম্বরটি এই ব্রাউজারে সংরক্ষিত আছে।', tone: 'ok' }
  if (!input) return { text: 'আবেদন, ফি দেওয়া ও আমার আবেদন দেখতে নম্বরটি লাগবে।', tone: 'info' }
  if (/[^0-9]/.test(input)) return { text: 'শুধু সংখ্যা দিন (বাংলা বা ইংরেজি)।', tone: 'error' }
  if (input.length > LENGTH) {
    return { text: `নম্বরটি ${toBanglaDigits(LENGTH)} অঙ্কের বেশি হয়ে গেছে।`, tone: 'error' }
  }
  return { text: `আরও ${toBanglaDigits(LENGTH - input.length)}টি অঙ্ক দিন।`, tone: 'info' }
}

export function StudentIdField() {
  const { input, setInput, validId, inputRef } = useStudentId()
  const { text, tone } = hint(input, validId !== null)

  return (
    <div className="student-id">
      <label htmlFor="student-id">শিক্ষার্থীর জন্ম নিবন্ধন নম্বর</label>
      <input
        id="student-id"
        ref={inputRef}
        value={input}
        onChange={(event) => setInput(event.target.value)}
        inputMode="numeric"
        autoComplete="off"
        spellCheck={false}
        placeholder="১৭ অঙ্কের নম্বর"
        aria-invalid={tone === 'error'}
        aria-describedby="student-id-hint"
      />
      <small id="student-id-hint" className={`hint hint-${tone}`} aria-live="polite">
        {text}
      </small>
    </div>
  )
}

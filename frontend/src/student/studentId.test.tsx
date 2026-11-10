import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { StudentIdField } from '../components/StudentIdField'
import { toAsciiDigits, toBanglaDigits } from '../format'
import { StudentIdProvider } from './StudentIdProvider'
import { cleanStudentId, isValidStudentId } from './studentId'

describe('student ID validation', () => {
  it.each([
    ['20152691234567890', true],
    ['2015269123456789', false], // 16 digits
    ['201526912345678901', false], // 18 digits
    ['2015269123456789A', false],
    ['', false],
    ['２０１５２６９１２３４５６７８９０', false], // full-width digits are not ASCII
  ])('isValidStudentId(%j) is %s', (value, expected) => {
    expect(isValidStudentId(value)).toBe(expected)
  })

  it('converts Bangla digits and drops spaces and dashes', () => {
    expect(cleanStudentId('২০১৫ ২৬৯১-২৩৪৫৬৭৮৯০')).toBe('20152691234567890')
    expect(isValidStudentId(cleanStudentId('২০১৫২৬৯১২৩৪৫৬৭৮৯০'))).toBe(true)
  })
})

describe('Bangla digit conversion', () => {
  it('turns every Bangla digit into its ASCII digit and leaves the rest', () => {
    expect(toAsciiDigits('০১২৩৪৫৬৭৮৯')).toBe('0123456789')
    expect(toAsciiDigits('আসন ১২০টি')).toBe('আসন 120টি')
  })

  it('turns ASCII digits into Bangla digits', () => {
    expect(toBanglaDigits(1234567890)).toBe('১২৩৪৫৬৭৮৯০')
    expect(toBanglaDigits('09:58')).toBe('০৯:৫৮')
  })
})

describe('student ID box', () => {
  function renderField() {
    render(
      <StudentIdProvider>
        <StudentIdField />
      </StudentIdProvider>,
    )
    return screen.getByLabelText('শিক্ষার্থীর জন্ম নিবন্ধন নম্বর')
  }

  it('shows typed Bangla digits as ASCII and saves the valid ID', async () => {
    const input = renderField()
    await userEvent.type(input, '২০১৫২৬৯১২৩৪৫৬৭৮৯০')

    expect(input).toHaveValue('20152691234567890')
    expect(screen.getByText('✓ নম্বরটি এই ব্রাউজারে সংরক্ষিত আছে।')).toBeInTheDocument()
    expect(localStorage.getItem('admission.studentId')).toBe('20152691234567890')
  })

  it('says how many digits are missing', async () => {
    const input = renderField()
    await userEvent.type(input, '১২৩')
    expect(screen.getByText('আরও ১৪টি অঙ্ক দিন।')).toBeInTheDocument()
    expect(input).not.toHaveAttribute('aria-invalid', 'true')
  })

  it('flags letters and too many digits', async () => {
    const input = renderField()
    await userEvent.type(input, '2015ab')
    expect(screen.getByText('শুধু সংখ্যা দিন (বাংলা বা ইংরেজি)।')).toBeInTheDocument()
    expect(input).toHaveAttribute('aria-invalid', 'true')

    await userEvent.clear(input)
    await userEvent.type(input, '201526912345678901')
    expect(screen.getByText('নম্বরটি ১৭ অঙ্কের বেশি হয়ে গেছে।')).toBeInTheDocument()
  })

  it('restores the saved ID on the next visit', () => {
    localStorage.setItem('admission.studentId', '20152691234567890')
    expect(renderField()).toHaveValue('20152691234567890')
  })
})

import { ApiError, NetworkError } from './api/client'

export const NETWORK_MESSAGE =
  'সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। ইন্টারনেট সংযোগ ঠিক আছে কিনা দেখে একটু পরে আবার চেষ্টা করুন।'

/** Every code the backend sends, in plain Bangla. */
const MESSAGES: Record<string, string> = {
  MISSING_STUDENT_ID: 'শিক্ষার্থীর জন্ম নিবন্ধন নম্বর দেওয়া হয়নি। উপরের বক্সে নম্বরটি লিখুন।',
  INVALID_STUDENT_ID: 'জন্ম নিবন্ধন নম্বর ঠিক ১৭ অঙ্কের হতে হবে।',
  VALIDATION_ERROR: 'দেওয়া তথ্য সঠিক নয়। ক্লাসের নাম ও আসনসংখ্যা আবার দেখুন।',
  INVALID_JSON: 'অনুরোধটি সঠিকভাবে পাঠানো যায়নি। পাতাটি রিফ্রেশ করে আবার চেষ্টা করুন।',
  CLASS_NOT_FOUND: 'ক্লাসটি খুঁজে পাওয়া যায়নি। হয়তো লিংকটি ভুল।',
  CLASS_FULL: 'দুঃখিত, এই ক্লাসে আর কোনো আসন খালি নেই।',
  ALREADY_APPLIED: 'এই ক্লাসে এই শিক্ষার্থীর একটি চলমান আবেদন বা নিশ্চিত ভর্তি আগে থেকেই আছে।',
  HOLD_NOT_FOUND: 'আবেদনটি খুঁজে পাওয়া যায়নি। হয়তো লিংকটি ভুল।',
  NOT_YOUR_HOLD: 'এই আবেদনটি অন্য শিক্ষার্থীর। উপরের জন্ম নিবন্ধন নম্বরটি ঠিক আছে কিনা দেখুন।',
  HOLD_EXPIRED: 'আবেদনের সময় শেষ হয়ে গেছে, আসনটি ছেড়ে দেওয়া হয়েছে। চাইলে আবার আবেদন করতে পারেন।',
  HOLD_CANCELLED: 'আবেদনটি বাতিল করা হয়েছে, তাই এর জন্য ভর্তি ফি দেওয়া যাবে না।',
  HOLD_CONFIRMED: 'ভর্তি ইতিমধ্যে নিশ্চিত হয়ে গেছে, তাই এখন আর বাতিল করা যাবে না।',
  INTERNAL_ERROR: 'সার্ভারে একটি সমস্যা হয়েছে। একটু পরে আবার চেষ্টা করুন।',
  BAD_REQUEST: 'অনুরোধটি সঠিক নয়। পাতাটি রিফ্রেশ করে আবার চেষ্টা করুন।',
  NOT_FOUND: 'যা খুঁজছেন তা পাওয়া যায়নি।',
  METHOD_NOT_ALLOWED: 'এই কাজটি এখানে করা যায় না। পাতাটি রিফ্রেশ করে আবার চেষ্টা করুন।',
  PAYLOAD_TOO_LARGE: 'পাঠানো তথ্য অনেক বড়। একটু ছোট করে আবার চেষ্টা করুন।',
  UNSUPPORTED_MEDIA_TYPE: 'অনুরোধটি সঠিকভাবে পাঠানো যায়নি। পাতাটি রিফ্রেশ করে আবার চেষ্টা করুন।',
}

export function errorMessage(error: unknown): string {
  if (error instanceof NetworkError) return NETWORK_MESSAGE

  if (error instanceof ApiError) {
    // The server's English message says which field failed.
    if (error.code === 'VALIDATION_ERROR') {
      if (error.message.startsWith('totalSeats')) return 'আসনসংখ্যা ১ বা তার বেশি একটি পূর্ণ সংখ্যা হতে হবে।'
      if (error.message.startsWith('name must not be empty')) return 'ক্লাসের নাম দিন।'
      if (error.message.startsWith('name')) return 'ক্লাসের নামে একটি অগ্রহণযোগ্য অক্ষর আছে।'
    }

    const known = MESSAGES[error.code]
    if (known) return known
    if (error.status >= 500) return MESSAGES.INTERNAL_ERROR
    return `অপ্রত্যাশিত একটি সমস্যা হয়েছে (কোড: ${error.code})। পাতাটি রিফ্রেশ করে আবার চেষ্টা করুন।`
  }

  return 'অপ্রত্যাশিত একটি সমস্যা হয়েছে। পাতাটি রিফ্রেশ করে আবার চেষ্টা করুন।'
}

export function isApiError(error: unknown, ...codes: string[]): error is ApiError {
  return error instanceof ApiError && (codes.length === 0 || codes.includes(error.code))
}

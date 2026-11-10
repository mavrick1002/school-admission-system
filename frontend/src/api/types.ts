export type HoldStatus = 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED'

export interface ClassSummary {
  id: string
  name: string
  totalSeats: number
  heldSeats: number
  confirmedSeats: number
  availableSeats: number
}

export interface CreatedClass {
  id: string
  name: string
  totalSeats: number
}

export interface CreatedHold {
  holdId: string
  classId: string
  studentId: string
  status: HoldStatus
  expiresAt: string
  secondsRemaining: number
}

export interface Hold {
  holdId: string
  classId: string
  status: HoldStatus
  expiresAt: string
  /** Seconds left by the server's clock; 0 when the status is not HELD. */
  secondsRemaining: number
}

/** A hold plus when (performance.now()) the request for it started, for the countdown. */
export interface TimedHold extends Hold {
  requestedAt: number
}

export interface MyHold extends Hold {
  className: string
}

export interface ConfirmedHold {
  holdId: string
  status: HoldStatus
  secondsRemaining: number
}

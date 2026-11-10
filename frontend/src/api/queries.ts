import { useMutation, useQuery, type UseMutationOptions } from '@tanstack/react-query'
import { useCallback, useRef } from 'react'
import { ApiError, apiFetch } from './client'
import type { ClassSummary, Hold, MyHold, TimedHold } from './types'

// The student ID is part of every key, so switching students never shows the
// previous student's data. Invalidating by the first element covers all students.
export const queryKeys = {
  classes: (studentId: string | null) => ['classes', studentId] as const,
  class: (classId: string, studentId: string | null) => ['class', classId, studentId] as const,
  hold: (holdId: string, studentId: string | null) => ['hold', holdId, studentId] as const,
  myHolds: (studentId: string | null) => ['myHolds', studentId] as const,
}

export function useClasses(studentId: string | null) {
  return useQuery({
    queryKey: queryKeys.classes(studentId),
    queryFn: async ({ signal }) => {
      const classes = await apiFetch<ClassSummary[]>('/classes', { signal })
      // A broken answer must read as a server error, not as "no classes yet".
      if (!Array.isArray(classes)) {
        throw new ApiError(502, 'INTERNAL_ERROR', 'GET /classes did not return an array.')
      }
      return classes
    },
    refetchInterval: 3000,
  })
}

export function useClass(classId: string | undefined, studentId: string | null) {
  return useQuery({
    queryKey: queryKeys.class(classId ?? '', studentId),
    queryFn: ({ signal }) => apiFetch<ClassSummary>(`/classes/${encodeURIComponent(classId!)}`, { signal }),
    enabled: !!classId,
    staleTime: 60_000,
  })
}

export function useHold(holdId: string, studentId: string | null) {
  return useQuery({
    queryKey: queryKeys.hold(holdId, studentId),
    queryFn: async ({ signal }): Promise<TimedHold> => {
      // Taken before the request, so the countdown never runs longer than the server's.
      const requestedAt = performance.now()
      const hold = await apiFetch<Hold>(`/holds/${encodeURIComponent(holdId)}`, { signal })
      return { ...hold, requestedAt }
    },
    // While HELD, re-sync with the server now and then (it may be paid in another tab).
    refetchInterval: (query) => (query.state.data?.status === 'HELD' ? 5000 : false),
  })
}

export function useMyHolds(studentId: string | null) {
  return useQuery({
    queryKey: queryKeys.myHolds(studentId),
    queryFn: ({ signal }) => apiFetch<MyHold[]>('/me/holds', { studentId, signal }),
    enabled: !!studentId,
    refetchInterval: 5000,
  })
}

/**
 * useMutation that ignores a second submit while the first is still in flight.
 * isPending only disables the button after a re-render; the ref also stops a fast
 * double click that lands before it.
 */
export function useGuardedMutation<TData, TVariables = void>(
  options: UseMutationOptions<TData, Error, TVariables>,
) {
  const busy = useRef(false)
  const mutation = useMutation<TData, Error, TVariables>(options)
  const { mutate } = mutation

  const run = useCallback(
    (variables: TVariables) => {
      if (busy.current) return
      busy.current = true
      mutate(variables, {
        onSettled: () => {
          busy.current = false
        },
      })
    },
    [mutate],
  )

  return { ...mutation, run }
}

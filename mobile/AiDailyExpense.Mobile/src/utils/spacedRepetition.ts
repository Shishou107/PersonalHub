import { StudyRating } from '../types/study';

export interface SpacedRepetitionOfflineResult {
  intervalDays: number;
  easeFactor: number;
  nextReviewDate: string;
  difficulty: 'Weak' | 'Good' | 'Easy' | 'Mastered';
}

/**
 * SuperMemo SM-2 Customized Algorithm for Offline-First Mobile Study.
 * Exactly matches Backend ASP.NET Core logic:
 * - Good: 12h initial checkpoint, then smooth expansion
 * - Easy: 7-step expansion: 3 -> 7 -> 16 -> 35 -> 80 -> 180 -> 365 days (Mastered)
 * - Hard: 15 minutes consolidation
 * - Forgot: immediate retry
 */
export function calculateNextReviewOffline(
  rating: StudyRating,
  currentIntervalDays: number = 0,
  currentEaseFactor: number = 2.5,
  nowMs: number = Date.now(),
): SpacedRepetitionOfflineResult {
  const ef = Math.max(1.3, Math.min(3.0, currentEaseFactor || 2.5));

  switch (rating) {
    case 'Forgot': {
      const nextEf = Math.max(1.3, ef - 0.2);
      return {
        intervalDays: 0,
        easeFactor: Number(nextEf.toFixed(2)),
        nextReviewDate: new Date(nowMs).toISOString(),
        difficulty: 'Weak',
      };
    }

    case 'Hard': {
      const nextEf = Math.max(1.3, ef - 0.15);
      return {
        intervalDays: 0,
        easeFactor: Number(nextEf.toFixed(2)),
        nextReviewDate: new Date(nowMs + 15 * 60 * 1000).toISOString(), // 15 mins
        difficulty: 'Weak',
      };
    }

    case 'Good': {
      if (currentIntervalDays <= 0) {
        // Mốc nửa ngày (12 tiếng) cho lần đầu
        return {
          intervalDays: 0,
          easeFactor: Number(ef.toFixed(2)),
          nextReviewDate: new Date(nowMs + 12 * 60 * 60 * 1000).toISOString(),
          difficulty: 'Good',
        };
      }

      let nextInterval = currentIntervalDays === 1 ? 3 : Math.max(currentIntervalDays + 1, Math.round(currentIntervalDays * 1.5));
      return {
        intervalDays: nextInterval,
        easeFactor: Number(ef.toFixed(2)),
        nextReviewDate: new Date(nowMs + nextInterval * 86400000).toISOString(),
        difficulty: nextInterval >= 365 ? 'Mastered' : 'Good',
      };
    }

    case 'Easy': {
      const nextEf = Math.min(3.0, ef + 0.15);
      let nextInterval: number;

      if (currentIntervalDays <= 0) nextInterval = 3;
      else if (currentIntervalDays < 7) nextInterval = 7;
      else if (currentIntervalDays < 16) nextInterval = 16;
      else if (currentIntervalDays < 35) nextInterval = 35;
      else if (currentIntervalDays < 80) nextInterval = 80;
      else if (currentIntervalDays < 180) nextInterval = 180;
      else nextInterval = 365;

      return {
        intervalDays: nextInterval,
        easeFactor: Number(nextEf.toFixed(2)),
        nextReviewDate: new Date(nowMs + nextInterval * 86400000).toISOString(),
        difficulty: nextInterval >= 365 ? 'Mastered' : 'Easy',
      };
    }

    default: {
      return {
        intervalDays: Math.max(1, currentIntervalDays),
        easeFactor: Number(ef.toFixed(2)),
        nextReviewDate: new Date(nowMs + 86400000).toISOString(),
        difficulty: 'Good',
      };
    }
  }
}

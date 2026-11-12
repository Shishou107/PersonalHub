import { db } from './db';
import { StudyCard, StudyDeck, StudyRating } from '../types/study';

export interface PendingReview {
  id: number;
  cardId: number;
  rating: StudyRating;
  createdAt: string;
}

export const studyRepo = {
  // ==================== BỘ THẺ (DECKS) ====================

  /**
   * Lưu danh sách bộ thẻ từ Backend vào SQLite
   */
  saveDecks: (decks: StudyDeck[]) => {
    try {
      db.withTransactionSync(() => {
        db.runSync('DELETE FROM local_study_decks');
        for (const deck of decks) {
          db.runSync(
            `INSERT INTO local_study_decks (id, name, description, language, cardCount, dueCount, createdAt)
             VALUES (?, ?, ?, ?, ?, ?, ?)`,
            [
              deck.id,
              deck.name,
              deck.description || '',
              deck.language || 'english',
              deck.cardCount || 0,
              deck.dueCount || 0,
              deck.createdAt || '',
            ]
          );
        }
      });
    } catch (e) {
      console.error('[studyRepo] Lỗi khi lưu Decks vào SQLite:', e);
    }
  },

  /**
   * Lấy danh sách bộ thẻ từ SQLite (khi Offline)
   */
  getDecks: (): StudyDeck[] => {
    try {
      return (db.getAllSync('SELECT * FROM local_study_decks ORDER BY name ASC') || []) as StudyDeck[];
    } catch (e) {
      console.error('[studyRepo] Lỗi khi đọc Decks từ SQLite:', e);
      return [];
    }
  },

  // ==================== THẺ TỪ VỰNG (CARDS) ====================

  /**
   * Lưu danh sách thẻ từ vựng vào SQLite
   */
  saveCards: (cards: StudyCard[], isToday = false) => {
    try {
      db.withTransactionSync(() => {
        for (const card of cards) {
          db.runSync(
            `INSERT OR REPLACE INTO local_study_cards (
              id, deckId, deckName, cardType, frontText, backText, example, note,
              pronunciation, tags, extendedData, normalizedText, difficulty,
              createdAt, updatedAt, lastReviewDate, nextReviewDate,
              intervalDays, reviewCount, lastRating, isTodayCard
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
            [
              card.id,
              card.deckId,
              card.deckName || '',
              card.cardType || 'Vocabulary',
              card.frontText,
              card.backText,
              card.example || '',
              card.note || '',
              card.pronunciation || '',
              card.tags || '',
              card.extendedData || '',
              card.normalizedText || '',
              card.difficulty || 'New',
              card.createdAt || '',
              card.updatedAt || '',
              card.lastReviewDate || '',
              card.nextReviewDate || '',
              card.intervalDays || 0,
              card.reviewCount || 0,
              card.lastRating || '',
              isToday ? 1 : 0,
            ]
          );
        }
      });
    } catch (e) {
      console.error('[studyRepo] Lỗi khi lưu Cards vào SQLite:', e);
    }
  },

  /**
   * Lấy danh sách thẻ của 1 bộ thẻ từ SQLite (khi Offline)
   */
  getCards: (deckId?: number): StudyCard[] => {
    try {
      if (deckId) {
        return (db.getAllSync(
          'SELECT * FROM local_study_cards WHERE deckId = ? ORDER BY id DESC',
          [deckId]
        ) || []) as StudyCard[];
      }
      return (db.getAllSync('SELECT * FROM local_study_cards ORDER BY id DESC') || []) as StudyCard[];
    } catch (e) {
      console.error('[studyRepo] Lỗi khi đọc Cards từ SQLite:', e);
      return [];
    }
  },

  /**
   * Lấy danh sách thẻ cần ôn tập hôm nay từ SQLite (khi Offline)
   */
  getTodayCards: (deckId?: number): StudyCard[] => {
    try {
      if (deckId) {
        return (db.getAllSync(
          'SELECT * FROM local_study_cards WHERE deckId = ? AND (isTodayCard = 1 OR nextReviewDate <= datetime("now")) ORDER BY id ASC',
          [deckId]
        ) || []) as StudyCard[];
      }
      return (db.getAllSync(
        'SELECT * FROM local_study_cards WHERE isTodayCard = 1 OR nextReviewDate <= datetime("now") ORDER BY id ASC'
      ) || []) as StudyCard[];
    } catch (e) {
      console.error('[studyRepo] Lỗi khi đọc TodayCards từ SQLite:', e);
      return [];
    }
  },

  // ==================== HÀNG ĐỢI ĐỒNG BỘ ÔN TẬP (PENDING REVIEWS) ====================

  /**
   * Lưu lượt ôn tập vào hàng đợi khi người dùng chấm điểm lúc Offline
   */
  addPendingReview: (cardId: number, rating: StudyRating) => {
    try {
      db.runSync(
        'INSERT INTO local_pending_reviews (cardId, rating, createdAt) VALUES (?, ?, ?)',
        [cardId, rating, new Date().toISOString()]
      );
      // Cập nhật tạm thời lastRating trong local card
      db.runSync(
        'UPDATE local_study_cards SET lastRating = ?, reviewCount = reviewCount + 1, lastReviewDate = ? WHERE id = ?',
        [rating, new Date().toISOString(), cardId]
      );
    } catch (e) {
      console.error('[studyRepo] Lỗi khi lưu Pending Review:', e);
    }
  },

  /**
   * Lấy danh sách các lượt ôn tập đang chờ gửi lên Server
   */
  getPendingReviews: (): PendingReview[] => {
    try {
      return (db.getAllSync('SELECT * FROM local_pending_reviews ORDER BY id ASC') || []) as PendingReview[];
    } catch (e) {
      console.error('[studyRepo] Lỗi khi lấy Pending Reviews:', e);
      return [];
    }
  },

  /**
   * Xóa lượt ôn tập đã gửi thành công lên Backend
   */
  removePendingReview: (id: number) => {
    try {
      db.runSync('DELETE FROM local_pending_reviews WHERE id = ?', [id]);
    } catch (e) {
      console.error('[studyRepo] Lỗi khi xóa Pending Review:', e);
    }
  },
};

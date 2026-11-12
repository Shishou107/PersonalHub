import api from './api';
import { ApiResponse } from '../types/api';
import {
  CreateStudyDeckPayload,
  CreateStudyCardPayload,
  StudyCard,
  StudyDeck,
  StudyRating,
  StudyReviewResult,
  StudyStats,
} from '../types/study';
import { studyRepo } from '../database/studyRepo';

/**
 * Tự động gửi bù các lượt ôn tập Offline lên Server khi có kết nối mạng
 */
const syncPendingReviews = async () => {
  try {
    const pending = studyRepo.getPendingReviews();
    if (pending.length === 0) return;

    console.log(`[Offline Sync] Đang gửi bù ${pending.length} lượt ôn tập lên Server...`);
    for (const item of pending) {
      try {
        await api.post(`/study/cards/${item.cardId}/review`, { rating: item.rating });
        studyRepo.removePendingReview(item.id);
      } catch (err) {
        // Nếu vẫn chưa có mạng ổn định thì dừng lại chờ lần sau
        break;
      }
    }
  } catch (e) {
    console.error('[Offline Sync] Lỗi khi đồng bộ review:', e);
  }
};

const studyService = {
  getStats: async (): Promise<ApiResponse<StudyStats>> => {
    try {
      const response = await api.get<ApiResponse<StudyStats>>('/study/stats');
      if (response.data.success && response.data.data) {
        // Tự động lưu bộ thẻ vào SQLite khi Online
        if (response.data.data.decks) {
          studyRepo.saveDecks(response.data.data.decks);
        }
        // Đồng bộ ngầm toàn bộ thẻ về SQLite để sẵn sàng Offline
        studyService.preloadAllCards();
        // Gửi bù review còn nợ
        syncPendingReviews();
      }
      return response.data;
    } catch (error) {
      console.log('[Study] Mất mạng -> Chuyển sang đọc Thống kê từ SQLite');
      // Fallback khi offline: tính sơ bộ từ dữ liệu SQLite cục bộ
      const localDecks = studyRepo.getDecks();
      const localToday = studyRepo.getTodayCards();
      const allCards = studyRepo.getCards();

      return {
        success: true,
        message: 'Dữ liệu ngoại tuyến (Offline)',
        data: {
          dueToday: localToday.length,
          reviewedToday: 0,
          totalCards: allCards.length,
          weakCards: 0,
          upcomingCards: 0,
          streakDays: 1,
          decks: localDecks,
        },
      };
    }
  },

  /**
   * Tải ngầm toàn bộ thẻ về lưu sẵn vào SQLite
   */
  preloadAllCards: async () => {
    try {
      const res = await api.get<ApiResponse<StudyCard[]>>('/study/cards');
      if (res.data.success && res.data.data) {
        studyRepo.saveCards(res.data.data);
      }
    } catch {
      // Bỏ qua lỗi khi tải ngầm
    }
  },

  getDecks: async (): Promise<ApiResponse<StudyDeck[]>> => {
    try {
      const response = await api.get<ApiResponse<StudyDeck[]>>('/study/decks');
      if (response.data.success && response.data.data) {
        // Lưu bản copy vào SQLite
        studyRepo.saveDecks(response.data.data);
        // Kích hoạt đồng bộ ngầm các review còn tồn
        syncPendingReviews();
      }
      return response.data;
    } catch (error) {
      console.log('[Study] Mất mạng -> Lấy danh sách Decks từ SQLite');
      const localDecks = studyRepo.getDecks();
      return {
        success: true,
        message: 'Dữ liệu bộ thẻ ngoại tuyến',
        data: localDecks,
      };
    }
  },

  createDeck: async (payload: CreateStudyDeckPayload): Promise<ApiResponse<StudyDeck>> => {
    const response = await api.post<ApiResponse<StudyDeck>>('/study/decks', payload);
    return response.data;
  },

  deleteDeck: async (id: number): Promise<ApiResponse<boolean>> => {
    const response = await api.post<ApiResponse<boolean>>(`/study/decks/${id}/delete`);
    return response.data;
  },

  getCards: async (deckId?: number): Promise<ApiResponse<StudyCard[]>> => {
    try {
      const url = deckId ? `/study/cards?deckId=${deckId}` : '/study/cards';
      const response = await api.get<ApiResponse<StudyCard[]>>(url);
      if (response.data.success && response.data.data) {
        // Lưu thẻ vào SQLite
        studyRepo.saveCards(response.data.data);
      }
      return response.data;
    } catch (error) {
      console.log(`[Study] Mất mạng -> Lấy danh sách Cards của Deck ${deckId} từ SQLite`);
      const localCards = studyRepo.getCards(deckId);
      return {
        success: true,
        message: 'Dữ liệu thẻ từ vựng ngoại tuyến',
        data: localCards,
      };
    }
  },

  createCard: async (payload: CreateStudyCardPayload): Promise<ApiResponse<StudyCard>> => {
    const response = await api.post<ApiResponse<StudyCard>>('/study/cards', payload);
    return response.data;
  },

  deleteCard: async (id: number): Promise<ApiResponse<boolean>> => {
    const response = await api.post<ApiResponse<boolean>>(`/study/cards/${id}/delete`);
    return response.data;
  },

  getTodayCards: async (deckId?: number): Promise<ApiResponse<StudyCard[]>> => {
    try {
      const url = deckId ? `/study/today?deckId=${deckId}` : '/study/today';
      const response = await api.get<ApiResponse<StudyCard[]>>(url);
      if (response.data.success && response.data.data) {
        // Đánh dấu và lưu thẻ ôn hôm nay vào SQLite
        studyRepo.saveCards(response.data.data, true);
        syncPendingReviews();
      }
      return response.data;
    } catch (error) {
      console.log(`[Study] Mất mạng -> Lấy thẻ cần ôn hôm nay từ SQLite`);
      const localTodayCards = studyRepo.getTodayCards(deckId);
      return {
        success: true,
        message: 'Danh sách thẻ ôn tập ngoại tuyến',
        data: localTodayCards,
      };
    }
  },

  reviewCard: async (id: number, rating: StudyRating): Promise<ApiResponse<StudyReviewResult>> => {
    try {
      const response = await api.post<ApiResponse<StudyReviewResult>>(`/study/cards/${id}/review`, { rating });
      return response.data;
    } catch (error) {
      console.log(`[Study] Mất mạng -> Lưu lượt chấm điểm [${rating}] của thẻ #${id} vào Hàng đợi SQLite`);
      studyRepo.addPendingReview(id, rating);

      // Trả về kết quả tạm thời để UI tiếp tục chuyển sang thẻ tiếp theo mượt mà
      return {
        success: true,
        message: 'Đã lưu kết quả ôn tập ngoại tuyến (sẽ đồng bộ khi có mạng)',
        data: {
          cardId: id,
          rating,
          intervalDays: 1,
          nextReviewDate: new Date(Date.now() + 86400000).toISOString(),
          reviewCount: 1,
        },
      };
    }
  },
};

export default studyService;

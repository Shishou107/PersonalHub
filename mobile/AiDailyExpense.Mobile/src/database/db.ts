import * as SQLite from 'expo-sqlite';

// Mở database SQLite cục bộ trên thiết bị an toàn (có fallback cho Web)
let localDb: any = null;

try {
  localDb = SQLite.openDatabaseSync('AiDailyExpense.db');
} catch (error) {
  console.warn('[SQLite] Không thể mở SQLite cục bộ (Web fallback):', error);
  localDb = {
    execAsync: async () => {},
    runSync: () => {},
    withTransactionSync: (fn: () => void) => {
      try { fn(); } catch (e) { console.warn('[SQLite Mock Transaction Error]', e); }
    },
    getAllSync: () => [],
    getFirstSync: () => null,
  };
}

export const db = localDb;

/**
 * Khởi tạo các bảng dữ liệu cho tính năng Học từ vựng (Offline Flashcard)
 */
export const initLocalDatabase = async (): Promise<void> => {
  try {
    await db.execAsync(`
      PRAGMA journal_mode = WAL;

      -- 1. Bảng lưu trữ Bộ thẻ (Study Decks)
      CREATE TABLE IF NOT EXISTS local_study_decks (
        id INTEGER PRIMARY KEY,
        name TEXT NOT NULL,
        description TEXT,
        language TEXT DEFAULT 'english',
        cardCount INTEGER DEFAULT 0,
        dueCount INTEGER DEFAULT 0,
        createdAt TEXT
      );

      -- 2. Bảng lưu trữ Thẻ từ vựng (Study Cards)
      CREATE TABLE IF NOT EXISTS local_study_cards (
        id INTEGER PRIMARY KEY,
        deckId INTEGER,
        deckName TEXT,
        cardType TEXT DEFAULT 'Vocabulary',
        frontText TEXT NOT NULL,
        backText TEXT NOT NULL,
        example TEXT,
        note TEXT,
        pronunciation TEXT,
        tags TEXT,
        extendedData TEXT,
        normalizedText TEXT,
        difficulty TEXT DEFAULT 'New',
        createdAt TEXT,
        updatedAt TEXT,
        lastReviewDate TEXT,
        nextReviewDate TEXT,
        intervalDays INTEGER DEFAULT 0,
        reviewCount INTEGER DEFAULT 0,
        lastRating TEXT,
        isTodayCard INTEGER DEFAULT 0
      );

      -- 3. Hàng đợi lưu kết quả ôn tập khi Offline (Pending Reviews)
      CREATE TABLE IF NOT EXISTS local_pending_reviews (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        cardId INTEGER NOT NULL,
        rating TEXT NOT NULL,
        createdAt TEXT NOT NULL
      );
    `);

    console.log('[SQLite] Đã khởi tạo thành công Database Flashcard cục bộ!');
  } catch (error) {
    console.error('[SQLite] Lỗi khi khởi tạo Database:', error);
  }
};

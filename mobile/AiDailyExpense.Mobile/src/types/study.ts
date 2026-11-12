export type StudyCardType = 'Vocabulary' | 'Grammar' | 'QA';
export type StudyRating = 'Forgot' | 'Hard' | 'Good' | 'Easy';

export interface StudyDeck {
  id: number;
  name: string;
  description?: string;
  language: string;
  cardCount: number;
  dueCount: number;
  createdAt: string;
}

export interface CreateStudyDeckPayload {
  name: string;
  description?: string;
  language?: string;
}

export interface StudyCard {
  id: number;
  deckId: number;
  deckName: string;
  cardType: StudyCardType;
  frontText: string;
  backText: string;
  example?: string;
  note?: string;
  pronunciation?: string;
  tags?: string;
  extendedData?: string;
  normalizedText?: string;
  difficulty: string;
  createdAt: string;
  updatedAt?: string;
  lastReviewDate?: string;
  nextReviewDate?: string;
  intervalDays: number;
  reviewCount: number;
  lastRating?: StudyRating;
}

export interface CreateStudyCardPayload {
  deckId: number;
  cardType: StudyCardType;
  frontText: string;
  backText: string;
  example?: string;
  note?: string;
  pronunciation?: string;
  tags?: string;
  extendedData?: string;
  normalizedText?: string;
  difficulty?: string;
}

export interface StudyReviewResult {
  cardId: number;
  rating: StudyRating;
  intervalDays: number;
  nextReviewDate: string;
  reviewCount: number;
}

export interface StudyStats {
  dueToday: number;
  reviewedToday: number;
  totalCards: number;
  weakCards: number;
  upcomingCards: number;
  streakDays: number;
  decks: StudyDeck[];
}

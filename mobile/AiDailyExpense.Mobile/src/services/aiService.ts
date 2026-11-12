import api from './api';
import { ApiResponse } from '../types/api';

export interface AiParseResponseData {
  intent: 'add_expense' | 'add_task' | 'query_report' | 'unknown';
  savedItems?: Array<{
    expenseId: number;
    title: string;
    amount: number;
    categoryName: string;
    categoryType: 'Expense' | 'Income';
    paymentMethod: 'Cash' | 'Transfer';
  }>;
  task?: {
    taskId: number;
    title: string;
    startAt?: string;
    endAt?: string;
    dueAt?: string;
    status: string;
    taskType?: 'Task' | 'Schedule';
    location?: string;
    description?: string;
  };
  savedTasks?: Array<{
    taskId: number;
    title: string;
    startAt?: string;
    endAt?: string;
    dueAt?: string;
    status: string;
    taskType?: 'Task' | 'Schedule';
    location?: string;
    description?: string;
  }>;
}

export interface AiTimetableImageResponseData {
  intent: 'add_tasks_from_timetable';
  detectedMode: 'day' | 'week' | 'unknown';
  ignoredItems: number;
  savedTasks: Array<{
    taskId: number;
    title: string;
    description?: string;
    startAt?: string;
    endAt?: string;
    dueAt?: string;
    status: string;
  }>;
}

export interface AiVocabularyResponseData {
  word: string;
  language: string;
  translation: string;
  partOfSpeech: string;
  pronunciation: string;
  example: string;
  exampleTranslation: string;
  note: string;
  tags: string[];
}

export interface AiVocabularyAnalyzeResponseData {
  language: string;
  input: string;
  meaningVi: string;
  baseWord: string;
  wordFamily: {
    noun: string[];
    verb: string[];
    adjective: string[];
    adverb: string[];
  };
  synonyms: Array<{
    word: string;
    reading?: string;
    meaningVi: string;
    note: string;
  }>;
}

const aiService = {
  parseText: async (text: string): Promise<ApiResponse<AiParseResponseData>> => {
    const response = await api.post<ApiResponse<AiParseResponseData>>('/ai/parse', { text });
    return response.data;
  },

  parseTimetableImage: async (image: { uri: string; name?: string; type?: string }): Promise<ApiResponse<AiTimetableImageResponseData>> => {
    const formData = new FormData();
    formData.append('image', {
      uri: image.uri,
      name: image.name || 'timetable.jpg',
      type: image.type || 'image/jpeg',
    } as any);

    const response = await api.post<ApiResponse<AiTimetableImageResponseData>>(
      '/ai/parse-timetable-image',
      formData,
      {
        headers: { 'Content-Type': 'multipart/form-data' },
        timeout: 70000,
      }
    );
    return response.data;
  },

  analyzeVocabulary: async (text: string, instruction?: string): Promise<ApiResponse<AiVocabularyResponseData>> => {
    const response = await api.post<ApiResponse<AiVocabularyResponseData>>(
      '/Ai/vocabulary',
      { text, instruction },
      { timeout: 90000 }
    );
    return response.data;
  },

  analyzeVocabularyExtended: async (deckId: number, word: string, instruction?: string): Promise<ApiResponse<AiVocabularyAnalyzeResponseData>> => {
    const response = await api.post<ApiResponse<AiVocabularyAnalyzeResponseData>>(
      '/Ai/vocabulary/analyze',
      { deckId, word, instruction },
      { timeout: 90000 }
    );
    const payload: any = response.data;
    if (payload?.data) {
      const data = payload.data;
      const family = data.wordFamily || data.word_family || {};
      payload.data = {
        language: data.language || '',
        input: data.input || '',
        meaningVi: data.meaningVi || data.meaning_vi || '',
        baseWord: data.baseWord || data.base_word || '',
        wordFamily: {
          noun: family.noun || [],
          verb: family.verb || [],
          adjective: family.adjective || [],
          adverb: family.adverb || [],
        },
        synonyms: (data.synonyms || []).map((item: any) => ({
          word: item.word || '',
          reading: item.reading || '',
          meaningVi: item.meaningVi || item.meaning_vi || '',
          note: item.note || '',
        })),
      };
    }

    return payload;
  },
};

export default aiService;

import React, { useEffect, useMemo, useState } from 'react';
import { Alert, ScrollView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import PageContainer from '../../components/PageContainer';
import Loading from '../../components/Loading';
import studyService from '../../services/studyService';
import studySpeechService from '../../services/studySpeechService';
import type { AiVocabularyAnalyzeResponseData } from '../../services/aiService';
import { StudyCard, StudyDeck, StudyRating } from '../../types/study';
import { COLORS } from '../../utils/theme';
import { getApiErrorMessage } from '../../utils/apiError';

const ratingButtons: { rating: StudyRating; label: string; color: string }[] = [
  { rating: 'Forgot', label: 'Quên', color: '#B91C1C' },
  { rating: 'Hard', label: 'Khó', color: '#D97706' },
  { rating: 'Good', label: 'Nhớ tạm', color: '#2563EB' },
  { rating: 'Easy', label: 'Dễ', color: '#059669' },
];

const StudyReviewScreen = ({ navigation, route }: any) => {
  const initialDeckId = route.params?.deckId as number | undefined;
  const initialDeckName = route.params?.deckName as string | undefined;
  const [decks, setDecks] = useState<StudyDeck[]>([]);
  const [selectedDeck, setSelectedDeck] = useState<StudyDeck | null>(
    initialDeckId ? {
      id: initialDeckId,
      name: initialDeckName || 'Bộ thẻ',
      language: 'other',
      cardCount: 0,
      dueCount: 0,
      createdAt: '',
    } : null,
  );
  const [cards, setCards] = useState<StudyCard[]>([]);
  const [index, setIndex] = useState(0);
  const [showAnswer, setShowAnswer] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const currentCard = cards[index];

  const progressText = useMemo(() => {
    if (cards.length === 0) return '0/0';
    return `${Math.min(index + 1, cards.length)}/${cards.length}`;
  }, [cards.length, index]);

  const loadDecks = async () => {
    try {
      const res = await studyService.getDecks();
      if (res.success && res.data) {
        setDecks(res.data);
        if (initialDeckId) {
          const matchedDeck = res.data.find((deck) => deck.id === initialDeckId);
          if (matchedDeck) {
            setSelectedDeck(matchedDeck);
          }
        }
      }
    } catch (e: any) {
      Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể tải bộ thẻ.'));
    }
  };

  const fetchCards = async (deck: StudyDeck) => {
    setLoading(true);
    try {
      const res = await studyService.getTodayCards(deck.id);
      if (res.success && res.data) {
        setCards(res.data);
        setIndex(0);
        setShowAnswer(false);
      }
    } catch (e: any) {
      Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể tải thẻ cần ôn.'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const prepare = async () => {
      setLoading(true);
      await loadDecks();
      if (selectedDeck) {
        await fetchCards(selectedDeck);
      } else {
        setLoading(false);
      }
    };

    const unsubscribe = navigation.addListener('focus', prepare);
    prepare();
    return unsubscribe;
  }, [navigation]);

  const chooseDeck = async (deck: StudyDeck) => {
    setSelectedDeck(deck);
    await fetchCards(deck);
  };

  const changeDeck = () => {
    setSelectedDeck(null);
    setCards([]);
    setIndex(0);
    setShowAnswer(false);
  };

  const containsJapanese = (value: string) => /[\u3040-\u30ff\u4e00-\u9fff]/.test(value);

  const getSpeechLanguage = () => {
    if (selectedDeck?.language === 'japanese' || containsJapanese(currentCard?.frontText || '')) {
      return 'japanese';
    }

    return 'english';
  };

  const getSpeechText = () => {
    const raw = currentCard?.frontText || '';
    if (getSpeechLanguage() !== 'japanese') {
      return raw;
    }

    return raw.replace(/\s*\([A-Za-z0-9\s.'-]+\)/g, '').trim();
  };

  const speakCurrentCard = async () => {
    if (!currentCard) return;

    const ok = await studySpeechService.speak(getSpeechText(), getSpeechLanguage());
    if (!ok) {
      Alert.alert('Giọng đọc', 'Máy chưa hỗ trợ đọc từ này.');
    }
  };

  const parseExtendedAnalysis = (value?: string): AiVocabularyAnalyzeResponseData | null => {
    if (!value) return null;

    try {
      const data: any = JSON.parse(value);
      const family = data.wordFamily || data.word_family || {};
      return {
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
    } catch {
      return null;
    }
  };

  const renderExtendedAnalysis = (analysis: AiVocabularyAnalyzeResponseData | null) => {
    if (!analysis) return null;

    const familyRows = [
      { label: 'Danh từ (Noun)', values: analysis.wordFamily?.noun || [] },
      { label: 'Động từ (Verb)', values: analysis.wordFamily?.verb || [] },
      { label: 'Tính từ (Adjective)', values: analysis.wordFamily?.adjective || [] },
    ].filter((row) => row.values.length > 0);

    const hasData = analysis.meaningVi || analysis.baseWord || familyRows.length > 0 || analysis.synonyms?.length > 0;
    if (!hasData) return null;

    return (
      <View style={styles.analysisCard}>
        <Text style={styles.analysisHeader}>Phân tích mở rộng</Text>
        {!!analysis.baseWord && <Text style={styles.analysisSummary}>Từ gốc: {analysis.baseWord}</Text>}
        {!!analysis.meaningVi && <Text style={styles.analysisSummary}>Nghĩa: {analysis.meaningVi}</Text>}

        {analysis.language === 'english' && familyRows.length > 0 ? (
          <View style={styles.analysisBlock}>
            <Text style={styles.analysisTitle}>Word Family (Danh từ - Động từ - Tính từ)</Text>
            {familyRows.map((row) => (
              <View key={row.label} style={styles.tableRow}>
                <Text style={styles.tableLabel}>{row.label}</Text>
                <Text style={styles.tableValue}>{row.values.join(', ')}</Text>
              </View>
            ))}
          </View>
        ) : null}

        {analysis.synonyms?.length > 0 ? (
          <View style={styles.analysisBlock}>
            <Text style={styles.analysisTitle}>Từ đồng nghĩa</Text>
            {analysis.synonyms.map((item, itemIndex) => (
              <View key={`${item.word}-${itemIndex}`} style={styles.synonymRow}>
                <Text style={styles.synonymWord}>{item.word || '-'}</Text>
                {!!item.reading && <Text style={styles.synonymReading}>{item.reading}</Text>}
                <Text style={styles.synonymMeaning}>{item.meaningVi || '-'}</Text>
                {!!item.note && <Text style={styles.synonymNote}>{item.note}</Text>}
              </View>
            ))}
          </View>
        ) : null}
      </View>
    );
  };

  const submitRating = async (rating: StudyRating) => {
    if (!currentCard) return;

    setSaving(true);
    try {
      await studyService.reviewCard(currentCard.id, rating);
      if (index + 1 >= cards.length) {
        if (selectedDeck) {
          await fetchCards(selectedDeck);
        } else {
          setCards([]);
          setIndex(0);
        }
      } else {
        setIndex(index + 1);
      }
      setShowAnswer(false);
    } catch (e: any) {
      Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể lưu kết quả ôn.'));
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <Loading fullScreen message="Đang chuẩn bị phiên ôn..." />;
  }

  if (!selectedDeck) {
    return (
      <PageContainer>
        <ScrollView contentContainerStyle={styles.chooseContent}>
          <Text style={styles.chooseTitle}>Chọn bộ thẻ để ôn</Text>
          <Text style={styles.chooseSub}>Mỗi phiên ôn chỉ lấy thẻ trong một bộ.</Text>

          <View style={styles.deckList}>
            {decks.length > 0 ? decks.map((deck) => (
              <TouchableOpacity key={deck.id} style={styles.deckRow} onPress={() => chooseDeck(deck)}>
                <View style={styles.deckIcon}>
                  <Text style={styles.deckIconText}>{deck.name.slice(0, 1).toUpperCase()}</Text>
                </View>
                <View style={styles.deckBody}>
                  <Text style={styles.deckName} numberOfLines={1}>{deck.name}</Text>
                  <Text style={styles.deckDesc} numberOfLines={1}>{deck.cardCount} thẻ trong bộ</Text>
                </View>
                <View style={styles.deckMeta}>
                  <Text style={styles.deckDue}>{deck.dueCount}</Text>
                  <Text style={styles.deckMetaLabel}>cần ôn</Text>
                </View>
              </TouchableOpacity>
            )) : (
              <View style={styles.emptyBox}>
                <Text style={styles.emptyText}>Chưa có bộ thẻ nào.</Text>
              </View>
            )}
          </View>
        </ScrollView>
      </PageContainer>
    );
  }

  if (!currentCard) {
    return (
      <PageContainer>
        <View style={styles.doneContainer}>
          <Text style={styles.doneTitle}>Bộ này xong rồi</Text>
          <Text style={styles.doneText}>Không còn thẻ cần ôn trong "{selectedDeck.name}".</Text>
          <TouchableOpacity style={styles.primaryButton} onPress={changeDeck}>
            <Text style={styles.primaryButtonText}>Chọn bộ khác</Text>
          </TouchableOpacity>
          <TouchableOpacity style={styles.secondaryButton} onPress={() => navigation.navigate('StudyHome')}>
            <Text style={styles.secondaryButtonText}>Về trang học</Text>
          </TouchableOpacity>
        </View>
      </PageContainer>
    );
  }

  return (
    <PageContainer>
      <View style={styles.content}>
        <View style={styles.topRow}>
          <TouchableOpacity style={styles.deckSwitch} onPress={changeDeck}>
            <Text style={styles.deckSwitchText} numberOfLines={1}>{selectedDeck.name}</Text>
          </TouchableOpacity>
          <Text style={styles.progress}>{progressText}</Text>
        </View>

        <ScrollView
          style={styles.studyCard}
          contentContainerStyle={[styles.studyCardContent, !showAnswer && styles.studyCardContentCentered]}
          showsVerticalScrollIndicator={false}
        >
          <Text style={styles.cardType}>
            {currentCard.cardType === 'Vocabulary' ? 'Từ vựng' : currentCard.cardType === 'Grammar' ? 'Cấu trúc' : 'Hỏi đáp'}
          </Text>
          <View style={styles.frontRow}>
            <Text style={styles.front}>{currentCard.frontText}</Text>
            <TouchableOpacity style={styles.speakButton} onPress={speakCurrentCard} activeOpacity={0.82}>
              <Ionicons name="volume-high-outline" size={22} color={COLORS.primary} />
            </TouchableOpacity>
          </View>
          {currentCard.pronunciation ? <Text style={styles.pronunciation}>{currentCard.pronunciation}</Text> : null}

          {showAnswer ? (
            <View style={styles.answerBox}>
              <Text style={styles.answerLabel}>Đáp án</Text>
              <Text style={styles.answerText}>{currentCard.backText}</Text>
              {currentCard.example ? <Text style={styles.example}>{currentCard.example}</Text> : null}
              {currentCard.note ? <Text style={styles.note}>{currentCard.note}</Text> : null}
              {currentCard.tags ? <Text style={styles.tags}>{currentCard.tags}</Text> : null}
              {renderExtendedAnalysis(parseExtendedAnalysis(currentCard.extendedData))}
            </View>
          ) : (
            <TouchableOpacity style={styles.showButton} onPress={() => setShowAnswer(true)}>
              <Text style={styles.showButtonText}>Hiện đáp án</Text>
            </TouchableOpacity>
          )}
        </ScrollView>

        {showAnswer ? (
          <View style={styles.ratingGrid}>
            {ratingButtons.map((item) => (
              <TouchableOpacity
                key={item.rating}
                style={[styles.ratingButton, { borderColor: item.color }]}
                onPress={() => submitRating(item.rating)}
                disabled={saving}
              >
                <Text style={[styles.ratingText, { color: item.color }]}>{item.label}</Text>
              </TouchableOpacity>
            ))}
          </View>
        ) : null}
      </View>
    </PageContainer>
  );
};

const styles = StyleSheet.create({
  chooseContent: {
    padding: 16,
    paddingBottom: 96,
  },
  chooseTitle: {
    fontSize: 22,
    fontWeight: '900',
    color: COLORS.text,
  },
  chooseSub: {
    marginTop: 5,
    marginBottom: 14,
    fontSize: 13,
    fontWeight: '700',
    color: COLORS.textMuted,
  },
  deckList: {
    borderRadius: 8,
    backgroundColor: COLORS.cardBg,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    padding: 8,
  },
  deckRow: {
    minHeight: 70,
    borderRadius: 8,
    flexDirection: 'row',
    alignItems: 'center',
    padding: 8,
    marginBottom: 8,
    backgroundColor: 'rgba(255, 255, 255, 0.62)',
  },
  deckIcon: {
    width: 42,
    height: 42,
    borderRadius: 8,
    backgroundColor: COLORS.primary,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 10,
  },
  deckIconText: {
    color: '#FFFFFF',
    fontSize: 17,
    fontWeight: '900',
  },
  deckBody: {
    flex: 1,
    minWidth: 0,
  },
  deckName: {
    fontSize: 15,
    fontWeight: '900',
    color: COLORS.text,
  },
  deckDesc: {
    fontSize: 12,
    color: COLORS.textMuted,
    marginTop: 3,
    fontWeight: '700',
  },
  deckMeta: {
    minWidth: 56,
    alignItems: 'center',
  },
  deckDue: {
    fontSize: 18,
    fontWeight: '900',
    color: COLORS.primary,
  },
  deckMetaLabel: {
    fontSize: 10,
    color: COLORS.textMuted,
    fontWeight: '700',
  },
  content: {
    flex: 1,
    padding: 16,
    paddingBottom: 28,
  },
  topRow: {
    minHeight: 42,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 8,
  },
  deckSwitch: {
    flex: 1,
    minHeight: 36,
    borderRadius: 8,
    backgroundColor: COLORS.cardBg,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    justifyContent: 'center',
    paddingHorizontal: 10,
  },
  deckSwitchText: {
    fontSize: 13,
    color: COLORS.primary,
    fontWeight: '900',
  },
  progress: {
    minWidth: 54,
    textAlign: 'center',
    borderRadius: 8,
    overflow: 'hidden',
    paddingVertical: 6,
    paddingHorizontal: 10,
    backgroundColor: COLORS.cardBg,
    color: COLORS.primary,
    fontWeight: '900',
  },
  studyCard: {
    flex: 1,
    borderRadius: 8,
    backgroundColor: COLORS.cardBg,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
  },
  studyCardContent: {
    padding: 18,
    flexGrow: 1,
  },
  studyCardContentCentered: {
    justifyContent: 'center',
  },
  cardType: {
    alignSelf: 'flex-start',
    borderRadius: 8,
    overflow: 'hidden',
    backgroundColor: 'rgba(76, 101, 143, 0.12)',
    color: COLORS.secondary,
    paddingHorizontal: 9,
    paddingVertical: 5,
    fontSize: 12,
    fontWeight: '900',
    marginBottom: 18,
  },
  front: {
    flex: 1,
    minWidth: 0,
    fontSize: 28,
    lineHeight: 36,
    fontWeight: '900',
    color: COLORS.text,
  },
  frontRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  speakButton: {
    width: 44,
    height: 44,
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    backgroundColor: 'rgba(255, 255, 255, 0.68)',
  },
  pronunciation: {
    marginTop: 10,
    fontSize: 15,
    color: COLORS.primary,
    fontWeight: '800',
  },
  showButton: {
    minHeight: 50,
    borderRadius: 8,
    backgroundColor: COLORS.primary,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 28,
  },
  showButtonText: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '900',
  },
  answerBox: {
    marginTop: 26,
    borderTopWidth: 1,
    borderTopColor: COLORS.cardBorder,
    paddingTop: 18,
  },
  answerLabel: {
    color: COLORS.primary,
    fontSize: 12,
    fontWeight: '900',
    marginBottom: 8,
  },
  answerText: {
    fontSize: 18,
    lineHeight: 26,
    color: COLORS.text,
    fontWeight: '800',
  },
  example: {
    marginTop: 12,
    fontSize: 14,
    lineHeight: 21,
    color: COLORS.textMuted,
    fontStyle: 'italic',
  },
  note: {
    marginTop: 10,
    fontSize: 13,
    color: COLORS.text,
    fontWeight: '700',
  },
  tags: {
    marginTop: 10,
    color: COLORS.primary,
    fontWeight: '900',
  },
  analysisCard: {
    marginTop: 14,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    backgroundColor: 'rgba(255, 255, 255, 0.56)',
    padding: 10,
  },
  analysisHeader: {
    color: COLORS.text,
    fontSize: 14,
    fontWeight: '900',
    marginBottom: 6,
  },
  analysisSummary: {
    color: COLORS.text,
    fontSize: 12,
    fontWeight: '700',
    marginTop: 3,
  },
  analysisBlock: {
    marginTop: 8,
  },
  analysisTitle: {
    color: COLORS.primary,
    fontSize: 12,
    fontWeight: '900',
    marginBottom: 5,
  },
  tableRow: {
    flexDirection: 'row',
    borderTopWidth: 1,
    borderTopColor: COLORS.cardBorder,
    paddingVertical: 6,
    gap: 8,
  },
  tableLabel: {
    width: 76,
    color: COLORS.text,
    fontSize: 12,
    fontWeight: '900',
  },
  tableValue: {
    flex: 1,
    color: COLORS.text,
    fontSize: 12,
    fontWeight: '700',
  },
  synonymRow: {
    borderTopWidth: 1,
    borderTopColor: COLORS.cardBorder,
    paddingVertical: 7,
  },
  synonymWord: {
    color: COLORS.text,
    fontSize: 12,
    fontWeight: '900',
  },
  synonymReading: {
    color: COLORS.secondary,
    fontSize: 11,
    fontWeight: '800',
    marginTop: 2,
  },
  synonymMeaning: {
    color: COLORS.text,
    fontSize: 12,
    fontWeight: '700',
    marginTop: 2,
  },
  synonymNote: {
    color: COLORS.textMuted,
    fontSize: 11,
    fontWeight: '600',
    marginTop: 2,
  },
  ratingGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginTop: 12,
  },
  ratingButton: {
    width: '48.8%',
    minHeight: 48,
    borderRadius: 8,
    borderWidth: 1,
    backgroundColor: COLORS.cardBg,
    alignItems: 'center',
    justifyContent: 'center',
  },
  ratingText: {
    fontSize: 14,
    fontWeight: '900',
  },
  doneContainer: {
    flex: 1,
    padding: 20,
    alignItems: 'center',
    justifyContent: 'center',
  },
  doneTitle: {
    fontSize: 24,
    fontWeight: '900',
    color: COLORS.text,
  },
  doneText: {
    marginTop: 8,
    fontSize: 14,
    color: COLORS.textMuted,
    fontWeight: '700',
    textAlign: 'center',
  },
  primaryButton: {
    minHeight: 48,
    borderRadius: 8,
    backgroundColor: COLORS.primary,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 18,
    marginTop: 18,
  },
  primaryButtonText: {
    color: '#FFFFFF',
    fontWeight: '900',
  },
  secondaryButton: {
    minHeight: 44,
    borderRadius: 8,
    backgroundColor: COLORS.cardBg,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 18,
    marginTop: 10,
  },
  secondaryButtonText: {
    color: COLORS.text,
    fontWeight: '900',
  },
  emptyBox: {
    paddingVertical: 26,
    alignItems: 'center',
  },
  emptyText: {
    color: COLORS.textMuted,
    fontWeight: '700',
  },
});

export default StudyReviewScreen;

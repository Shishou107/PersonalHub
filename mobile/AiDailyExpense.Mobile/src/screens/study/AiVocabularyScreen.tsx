import React, { useEffect, useState } from 'react';
import { Alert, ScrollView, StyleSheet, Text, TextInput, TouchableOpacity, View } from 'react-native';
import PageContainer from '../../components/PageContainer';
import aiService, { AiVocabularyAnalyzeResponseData } from '../../services/aiService';
import studyService from '../../services/studyService';
import { StudyDeck } from '../../types/study';
import { COLORS } from '../../utils/theme';
import { getApiErrorMessage } from '../../utils/apiError';

const AiVocabularyScreen = ({ navigation, route }: any) => {
  const initialDeckId = route.params?.deckId as number | undefined;
  const [decks, setDecks] = useState<StudyDeck[]>([]);
  const [deckId, setDeckId] = useState<number | undefined>(initialDeckId);
  const [inputWord, setInputWord] = useState('');
  const [aiInstruction, setAiInstruction] = useState('');
  const [word, setWord] = useState('');
  const [translation, setTranslation] = useState('');
  const [partOfSpeech, setPartOfSpeech] = useState('');
  const [pronunciation, setPronunciation] = useState('');
  const [example, setExample] = useState('');
  const [exampleTranslation, setExampleTranslation] = useState('');
  const [note, setNote] = useState('');
  const [tags, setTags] = useState('');
  const [generating, setGenerating] = useState(false);
  const [analyzing, setAnalyzing] = useState(false);
  const [extendedAnalysis, setExtendedAnalysis] = useState<AiVocabularyAnalyzeResponseData | null>(null);
  const [extendedMessage, setExtendedMessage] = useState('');
  const [saving, setSaving] = useState(false);

  const fillSuggestion = (data: {
    word?: string;
    translation?: string;
    partOfSpeech?: string;
    pronunciation?: string;
    example?: string;
    exampleTranslation?: string;
    note?: string;
    tags?: string[];
  }) => {
    setWord(data.word || inputWord.trim());
    setTranslation(data.translation || '');
    setPartOfSpeech(data.partOfSpeech || '');
    setPronunciation(data.pronunciation || '');
    setExample(data.example || '');
    setExampleTranslation(data.exampleTranslation || '');
    setNote([data.partOfSpeech, data.note].filter(Boolean).join(' - '));
    setTags((data.tags || []).join(', '));
  };

  const buildLocalSuggestion = (text: string) => {
    const normalized = text.trim();
    const lower = normalized.toLowerCase();

    if (lower === 'わたし' || lower === '私' || lower === 'watashi') {
      return {
        word: normalized,
        translation: 'tôi',
        partOfSpeech: 'đại từ',
        pronunciation: 'わたし / watashi',
        example: 'わたしは学生です。',
        exampleTranslation: 'Tôi là học sinh/sinh viên.',
        note: 'Cách nói trung tính, lịch sự để tự xưng là tôi.',
        tags: ['Tiếng Nhật', 'N5', 'Pronoun'],
      };
    }

    return {
      word: normalized,
      translation: '',
      note: 'Chưa kết nối được AI. Bạn có thể nhập nghĩa rồi lưu thẻ.',
      tags: ['Vocabulary'],
    };
  };

  const clearSuggestion = () => {
    setWord('');
    setTranslation('');
    setPartOfSpeech('');
    setPronunciation('');
    setExample('');
    setExampleTranslation('');
    setNote('');
    setTags('');
    setExtendedAnalysis(null);
    setExtendedMessage('');
  };

  const applyExtendedAnalysis = (analysis: AiVocabularyAnalyzeResponseData) => {
    setExtendedAnalysis(analysis);
    setExtendedMessage('');

    // Tự động chuyển sang từ nguyên mẫu nếu AI phân tích được baseWord (VD: Stopped -> stop)
    if (analysis.baseWord?.trim()) {
      setWord(analysis.baseWord.trim());
    }

    if (!translation.trim() && analysis.meaningVi?.trim()) {
      setTranslation(analysis.meaningVi.trim());
    }
  };

  const requestExtendedAnalysis = async (targetWord: string, instruction?: string) => {
    if (!deckId) {
      throw new Error('Vui lòng chọn bộ thẻ.');
    }

    const res = await aiService.analyzeVocabularyExtended(deckId, targetWord, instruction);
    if (res.success && res.data) {
      applyExtendedAnalysis(res.data);
      return;
    }

    setExtendedMessage(res.message || 'Chưa có dữ liệu mở rộng.');
  };

  useEffect(() => {
    const loadDecks = async () => {
      try {
        const res = await studyService.getDecks();
        if (res.success && res.data) {
          setDecks(res.data);
          setDeckId(initialDeckId || res.data[0]?.id);
        }
      } catch (e: any) {
        Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể tải bộ thẻ.'));
      }
    };

    loadDecks();
  }, [initialDeckId]);

  const generateVocabulary = async () => {
    if (!inputWord.trim()) {
      Alert.alert('Thiếu từ vựng', 'Vui lòng nhập một từ hoặc cụm từ.');
      return;
    }

    setGenerating(true);
    clearSuggestion();
    const currentInstruction = aiInstruction.trim() || undefined;
    try {
      const res = await aiService.analyzeVocabulary(inputWord.trim(), currentInstruction);
      if (res.success && res.data) {
        fillSuggestion(res.data);
      }
    } catch {
      fillSuggestion(buildLocalSuggestion(inputWord));
    }

    if (deckId) {
      try {
        await requestExtendedAnalysis(inputWord.trim(), currentInstruction);
      } catch (e: any) {
        setExtendedMessage(getApiErrorMessage(e, 'Không thể phân tích mở rộng từ vựng.'));
      }
    }

    setGenerating(false);
  };

  const analyzeExtendedVocabulary = async () => {
    if (!deckId) {
      Alert.alert('Thiếu bộ thẻ', 'Vui lòng chọn bộ thẻ.');
      return;
    }

    const targetWord = (word.trim() || inputWord.trim());
    if (!targetWord) {
      Alert.alert('Thiếu từ vựng', 'Vui lòng nhập từ cần phân tích.');
      return;
    }

    setAnalyzing(true);
    setExtendedAnalysis(null);
    setExtendedMessage('');
    try {
      await requestExtendedAnalysis(targetWord, aiInstruction.trim() || undefined);
    } catch (e: any) {
      setExtendedMessage(getApiErrorMessage(e, 'Không thể phân tích mở rộng từ vựng.'));
    } finally {
      setAnalyzing(false);
    }
  };

  const hasExtendedData = (data: AiVocabularyAnalyzeResponseData | null) => {
    if (!data) return false;
    const family = data.wordFamily;
    return Boolean(
      data.meaningVi ||
      data.baseWord ||
      data.synonyms?.length ||
      family?.noun?.length ||
      family?.verb?.length ||
      family?.adjective?.length ||
      family?.adverb?.length
    );
  };

  const renderWordFamily = (analysis: AiVocabularyAnalyzeResponseData) => {
    const rows = [
      { label: 'Danh từ (Noun)', values: analysis.wordFamily?.noun || [] },
      { label: 'Động từ (Verb)', values: analysis.wordFamily?.verb || [] },
      { label: 'Tính từ (Adjective)', values: analysis.wordFamily?.adjective || [] },
    ].filter((row) => row.values.length > 0);

    if (rows.length === 0) return null;

    return (
      <View style={styles.analysisBlock}>
        <Text style={styles.analysisTitle}>Word Family (Danh từ - Động từ - Tính từ)</Text>
        {rows.map((row) => (
          <View key={row.label} style={styles.tableRow}>
            <Text style={styles.tableLabel}>{row.label}</Text>
            <Text style={styles.tableValue}>{row.values.join(', ')}</Text>
          </View>
        ))}
      </View>
    );
  };

  const renderSynonyms = (analysis: AiVocabularyAnalyzeResponseData) => {
    if (!analysis.synonyms?.length) return null;

    return (
      <View style={styles.analysisBlock}>
        <Text style={styles.analysisTitle}>Từ đồng nghĩa</Text>
        {analysis.synonyms.map((item, index) => (
          <View key={`${item.word}-${index}`} style={styles.synonymRow}>
            <Text style={styles.synonymWord}>{item.word || '-'}</Text>
            {!!item.reading && <Text style={styles.synonymReading}>{item.reading}</Text>}
            <Text style={styles.synonymMeaning}>{item.meaningVi || '-'}</Text>
            {!!item.note && <Text style={styles.synonymNote}>{item.note}</Text>}
          </View>
        ))}
      </View>
    );
  };

  const saveCard = async () => {
    if (!deckId) {
      Alert.alert('Thiếu bộ thẻ', 'Vui lòng chọn bộ thẻ.');
      return;
    }

    if (!word.trim() || !translation.trim()) {
      Alert.alert('Thiếu nội dung', 'Từ vựng và bản dịch không được để trống.');
      return;
    }

    setSaving(true);
    try {
      await studyService.createCard({
        deckId,
        cardType: 'Vocabulary',
        frontText: word.trim(),
        backText: translation.trim(),
        pronunciation: pronunciation.trim() || undefined,
        example: example.trim() || undefined,
        note: [partOfSpeech.trim(), note.trim(), exampleTranslation.trim()]
          .filter(Boolean)
          .join(' | ') || undefined,
        tags: tags.trim() || undefined,
        extendedData: extendedAnalysis ? JSON.stringify(extendedAnalysis) : undefined,
        normalizedText: (word.trim() || inputWord.trim()).toLowerCase(),
        difficulty: 'New',
      });
      navigation.goBack();
    } catch (e: any) {
      Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể lưu thẻ từ vựng.'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <PageContainer>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <Text style={styles.title}>AI từ vựng</Text>
        <Text style={styles.subtitle}>Nhập từ tiếng Anh, Nhật hoặc ngôn ngữ khác để tạo thẻ học.</Text>

        <Text style={styles.label}>Bộ thẻ</Text>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.deckPicker}>
          {decks.map((deck) => (
            <TouchableOpacity
              key={deck.id}
              style={[styles.deckChip, deckId === deck.id && styles.deckChipActive]}
              onPress={() => {
                setDeckId(deck.id);
                setExtendedAnalysis(null);
                setExtendedMessage('');
              }}
            >
              <Text style={[styles.deckChipText, deckId === deck.id && styles.deckChipTextActive]}>{deck.name}</Text>
            </TouchableOpacity>
          ))}
        </ScrollView>

        <Text style={styles.label}>Từ cần tra</Text>
        <View style={styles.searchRow}>
          <TextInput
            style={[styles.input, styles.searchInput]}
            value={inputWord}
            onChangeText={setInputWord}
            placeholder="responsibility / 勉強 / bonjour"
            placeholderTextColor={COLORS.textMuted}
          />
          <TouchableOpacity style={styles.aiButton} onPress={generateVocabulary} disabled={generating}>
            <Text style={styles.aiButtonText}>{generating ? '...' : 'AI'}</Text>
          </TouchableOpacity>
        </View>

        <Text style={styles.label}>Lưu ý cho AI</Text>
        <TextInput
          style={[styles.input, styles.instructionInput]}
          value={aiInstruction}
          onChangeText={setAiInstruction}
          placeholder="Ví dụ: Tiếng Nhật thì phiên âm ra hiragana/katakana kế bên."
          placeholderTextColor={COLORS.textMuted}
          multiline
        />

        {Boolean(extendedAnalysis || extendedMessage) && (
          <View style={styles.analysisCard}>
            <Text style={styles.analysisHeader}>Kết quả mở rộng</Text>
            {extendedMessage ? (
              <Text style={styles.analysisEmpty}>{extendedMessage}</Text>
            ) : extendedAnalysis && hasExtendedData(extendedAnalysis) ? (
              <>
                <View style={styles.analysisSummary}>
                  <Text style={styles.summaryText}>Ngôn ngữ: {extendedAnalysis.language || '-'}</Text>
                  {!!extendedAnalysis.baseWord && <Text style={styles.summaryText}>Từ gốc: {extendedAnalysis.baseWord}</Text>}
                  {!!extendedAnalysis.meaningVi && <Text style={styles.summaryText}>Nghĩa: {extendedAnalysis.meaningVi}</Text>}
                </View>
                {extendedAnalysis.language === 'english' && renderWordFamily(extendedAnalysis)}
                {renderSynonyms(extendedAnalysis)}
              </>
            ) : (
              <Text style={styles.analysisEmpty}>Chưa có dữ liệu mở rộng</Text>
            )}
          </View>
        )}

        <Text style={styles.label}>Từ vựng *</Text>
        <TextInput style={styles.input} value={word} onChangeText={setWord} placeholder="responsibility" placeholderTextColor={COLORS.textMuted} />

        <Text style={styles.label}>Bản dịch *</Text>
        <TextInput style={styles.input} value={translation} onChangeText={setTranslation} placeholder="trách nhiệm" placeholderTextColor={COLORS.textMuted} />

        <Text style={styles.label}>Loại từ</Text>
        <TextInput style={styles.input} value={partOfSpeech} onChangeText={setPartOfSpeech} placeholder="noun / danh từ" placeholderTextColor={COLORS.textMuted} />

        <Text style={styles.label}>Phiên âm</Text>
        <TextInput style={styles.input} value={pronunciation} onChangeText={setPronunciation} placeholder="/rɪˌspɒn.səˈbɪl.ə.ti/" placeholderTextColor={COLORS.textMuted} />

        <Text style={styles.label}>Ví dụ</Text>
        <TextInput style={[styles.input, styles.multiInput]} value={example} onChangeText={setExample} placeholder="I have a responsibility to finish this project." placeholderTextColor={COLORS.textMuted} multiline />

        <Text style={styles.label}>Dịch ví dụ</Text>
        <TextInput style={[styles.input, styles.multiInput]} value={exampleTranslation} onChangeText={setExampleTranslation} placeholder="Tôi có trách nhiệm hoàn thành dự án này." placeholderTextColor={COLORS.textMuted} multiline />

        <Text style={styles.label}>Ghi chú</Text>
        <TextInput style={styles.input} value={note} onChangeText={setNote} placeholder="hay dùng trong phỏng vấn" placeholderTextColor={COLORS.textMuted} />

        <Text style={styles.label}>Tag</Text>
        <TextInput style={styles.input} value={tags} onChangeText={setTags} placeholder="English, Interview, B1" placeholderTextColor={COLORS.textMuted} />

        <TouchableOpacity style={[styles.saveButton, saving && styles.disabledButton]} onPress={saveCard} disabled={saving}>
          <Text style={styles.saveButtonText}>{saving ? 'Đang lưu...' : 'Lưu thẻ từ vựng'}</Text>
        </TouchableOpacity>
      </ScrollView>
    </PageContainer>
  );
};

const styles = StyleSheet.create({
  content: {
    padding: 16,
    paddingBottom: 96,
  },
  title: {
    fontSize: 22,
    fontWeight: '900',
    color: COLORS.text,
  },
  subtitle: {
    marginTop: 5,
    marginBottom: 4,
    fontSize: 13,
    color: COLORS.textMuted,
    fontWeight: '700',
  },
  label: {
    fontSize: 13,
    fontWeight: '900',
    color: COLORS.text,
    marginBottom: 7,
    marginTop: 12,
  },
  deckPicker: {
    gap: 8,
    paddingRight: 16,
  },
  deckChip: {
    minHeight: 38,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    backgroundColor: COLORS.cardBg,
    justifyContent: 'center',
    paddingHorizontal: 12,
  },
  deckChipActive: {
    backgroundColor: COLORS.primary,
    borderColor: COLORS.primary,
  },
  deckChipText: {
    color: COLORS.text,
    fontWeight: '800',
    fontSize: 12,
  },
  deckChipTextActive: {
    color: '#FFFFFF',
  },
  searchRow: {
    flexDirection: 'row',
    gap: 8,
  },
  searchInput: {
    flex: 1,
  },
  input: {
    minHeight: 48,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: COLORS.border,
    backgroundColor: 'rgba(255, 255, 255, 0.82)',
    paddingHorizontal: 12,
    paddingVertical: 10,
    color: COLORS.text,
    fontSize: 14,
    fontWeight: '600',
  },
  multiInput: {
    minHeight: 84,
    textAlignVertical: 'top',
  },
  instructionInput: {
    minHeight: 74,
    textAlignVertical: 'top',
  },
  aiButton: {
    width: 58,
    minHeight: 48,
    borderRadius: 8,
    backgroundColor: COLORS.primary,
    alignItems: 'center',
    justifyContent: 'center',
  },
  aiButtonText: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '900',
  },
  extendedButton: {
    minHeight: 46,
    borderRadius: 8,
    backgroundColor: COLORS.secondary,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 12,
  },
  extendedButtonText: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '900',
  },
  analysisCard: {
    borderRadius: 8,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    backgroundColor: COLORS.cardBg,
    padding: 12,
    marginTop: 12,
  },
  analysisHeader: {
    fontSize: 15,
    fontWeight: '900',
    color: COLORS.text,
    marginBottom: 8,
  },
  analysisSummary: {
    gap: 4,
    marginBottom: 10,
  },
  summaryText: {
    color: COLORS.text,
    fontSize: 13,
    fontWeight: '700',
  },
  analysisBlock: {
    marginTop: 8,
  },
  analysisTitle: {
    color: COLORS.primary,
    fontSize: 13,
    fontWeight: '900',
    marginBottom: 6,
  },
  tableRow: {
    flexDirection: 'row',
    borderTopWidth: 1,
    borderTopColor: COLORS.cardBorder,
    paddingVertical: 8,
    gap: 10,
  },
  tableLabel: {
    width: 86,
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
    paddingVertical: 9,
  },
  synonymWord: {
    color: COLORS.text,
    fontSize: 13,
    fontWeight: '900',
  },
  synonymReading: {
    color: COLORS.secondary,
    fontSize: 12,
    fontWeight: '800',
    marginTop: 2,
  },
  synonymMeaning: {
    color: COLORS.text,
    fontSize: 12,
    fontWeight: '700',
    marginTop: 3,
  },
  synonymNote: {
    color: COLORS.textMuted,
    fontSize: 12,
    fontWeight: '600',
    marginTop: 3,
  },
  analysisEmpty: {
    color: COLORS.textMuted,
    fontSize: 13,
    fontWeight: '700',
  },
  saveButton: {
    minHeight: 50,
    borderRadius: 8,
    backgroundColor: COLORS.primary,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 20,
  },
  disabledButton: {
    opacity: 0.65,
  },
  saveButtonText: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '900',
  },
});

export default AiVocabularyScreen;

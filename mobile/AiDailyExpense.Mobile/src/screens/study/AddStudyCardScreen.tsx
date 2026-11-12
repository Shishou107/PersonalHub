import React, { useEffect, useState } from 'react';
import { Alert, ScrollView, StyleSheet, Text, TextInput, TouchableOpacity, View } from 'react-native';
import PageContainer from '../../components/PageContainer';
import studyService from '../../services/studyService';
import { StudyCardType, StudyDeck } from '../../types/study';
import { COLORS } from '../../utils/theme';
import { getApiErrorMessage } from '../../utils/apiError';

const cardTypes: { value: StudyCardType; label: string }[] = [
  { value: 'Vocabulary', label: 'Từ vựng' },
  { value: 'Grammar', label: 'Cấu trúc' },
  { value: 'QA', label: 'Hỏi đáp' },
];

const AddStudyCardScreen = ({ navigation, route }: any) => {
  const initialDeckId = route.params?.deckId as number | undefined;
  const [decks, setDecks] = useState<StudyDeck[]>([]);
  const [deckId, setDeckId] = useState<number | undefined>(initialDeckId);
  const [cardType, setCardType] = useState<StudyCardType>('Vocabulary');
  const [frontText, setFrontText] = useState('');
  const [backText, setBackText] = useState('');
  const [example, setExample] = useState('');
  const [note, setNote] = useState('');
  const [pronunciation, setPronunciation] = useState('');
  const [tags, setTags] = useState('');
  const [saving, setSaving] = useState(false);

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

  const submit = async () => {
    if (!deckId) {
      Alert.alert('Thiếu bộ thẻ', 'Vui lòng chọn bộ thẻ.');
      return;
    }

    if (!frontText.trim() || !backText.trim()) {
      Alert.alert('Thiếu nội dung', 'Mặt trước và mặt sau không được để trống.');
      return;
    }

    setSaving(true);
    try {
      await studyService.createCard({
        deckId,
        cardType,
        frontText: frontText.trim(),
        backText: backText.trim(),
        example: example.trim() || undefined,
        note: note.trim() || undefined,
        pronunciation: pronunciation.trim() || undefined,
        tags: tags.trim() || undefined,
        difficulty: 'New',
      });
      navigation.goBack();
    } catch (e: any) {
      Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể thêm thẻ.'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <PageContainer>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <Text style={styles.label}>Loại thẻ</Text>
        <View style={styles.segmentGroup}>
          {cardTypes.map((type) => (
            <TouchableOpacity
              key={type.value}
              style={[styles.segmentButton, cardType === type.value && styles.segmentActive]}
              onPress={() => setCardType(type.value)}
            >
              <Text style={[styles.segmentText, cardType === type.value && styles.segmentTextActive]}>{type.label}</Text>
            </TouchableOpacity>
          ))}
        </View>

        <Text style={styles.label}>Bộ thẻ</Text>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.deckPicker}>
          {decks.map((deck) => (
            <TouchableOpacity
              key={deck.id}
              style={[styles.deckChip, deckId === deck.id && styles.deckChipActive]}
              onPress={() => setDeckId(deck.id)}
            >
              <Text style={[styles.deckChipText, deckId === deck.id && styles.deckChipTextActive]}>{deck.name}</Text>
            </TouchableOpacity>
          ))}
        </ScrollView>

        <Text style={styles.label}>{cardType === 'Vocabulary' ? 'Từ / Câu hỏi *' : cardType === 'Grammar' ? 'Cấu trúc *' : 'Câu hỏi *'}</Text>
        <TextInput
          style={styles.input}
          value={frontText}
          onChangeText={setFrontText}
          placeholder="responsibility / used to + V / Index trong SQL dùng để làm gì?"
          placeholderTextColor={COLORS.textMuted}
        />

        <Text style={styles.label}>{cardType === 'Vocabulary' ? 'Nghĩa / Đáp án *' : cardType === 'Grammar' ? 'Ý nghĩa, công thức *' : 'Đáp án *'}</Text>
        <TextInput
          style={[styles.input, styles.multiInput]}
          value={backText}
          onChangeText={setBackText}
          placeholder="trách nhiệm / S + used to + V / Giúp tăng tốc truy vấn..."
          placeholderTextColor={COLORS.textMuted}
          multiline
        />

        <Text style={styles.label}>Ví dụ</Text>
        <TextInput
          style={styles.input}
          value={example}
          onChangeText={setExample}
          placeholder="I have a responsibility to finish this project."
          placeholderTextColor={COLORS.textMuted}
        />

        <Text style={styles.label}>Phát âm</Text>
        <TextInput
          style={styles.input}
          value={pronunciation}
          onChangeText={setPronunciation}
          placeholder="/rɪˌspɒn.səˈbɪl.ə.ti/"
          placeholderTextColor={COLORS.textMuted}
        />

        <Text style={styles.label}>Ghi chú</Text>
        <TextInput
          style={styles.input}
          value={note}
          onChangeText={setNote}
          placeholder="hay dùng trong phỏng vấn"
          placeholderTextColor={COLORS.textMuted}
        />

        <Text style={styles.label}>Tag</Text>
        <TextInput
          style={styles.input}
          value={tags}
          onChangeText={setTags}
          placeholder="English, Interview, B1"
          placeholderTextColor={COLORS.textMuted}
        />

        <TouchableOpacity style={[styles.saveButton, saving && styles.saveButtonDisabled]} onPress={submit} disabled={saving}>
          <Text style={styles.saveButtonText}>{saving ? 'Đang lưu...' : 'Lưu thẻ'}</Text>
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
  label: {
    fontSize: 13,
    fontWeight: '900',
    color: COLORS.text,
    marginBottom: 7,
    marginTop: 12,
  },
  segmentGroup: {
    flexDirection: 'row',
    backgroundColor: COLORS.cardBg,
    borderRadius: 8,
    padding: 3,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
  },
  segmentButton: {
    flex: 1,
    minHeight: 38,
    borderRadius: 6,
    alignItems: 'center',
    justifyContent: 'center',
  },
  segmentActive: {
    backgroundColor: COLORS.primary,
  },
  segmentText: {
    fontSize: 12,
    fontWeight: '800',
    color: COLORS.text,
  },
  segmentTextActive: {
    color: '#FFFFFF',
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
    minHeight: 92,
    textAlignVertical: 'top',
  },
  saveButton: {
    minHeight: 50,
    borderRadius: 8,
    backgroundColor: COLORS.primary,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 20,
  },
  saveButtonDisabled: {
    opacity: 0.65,
  },
  saveButtonText: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '900',
  },
});

export default AddStudyCardScreen;

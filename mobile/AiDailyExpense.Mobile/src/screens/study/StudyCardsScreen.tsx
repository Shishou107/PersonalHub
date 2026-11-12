import React, { useEffect, useState } from 'react';
import { Alert, RefreshControl, ScrollView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import PageContainer from '../../components/PageContainer';
import Loading from '../../components/Loading';
import studyService from '../../services/studyService';
import { StudyCard } from '../../types/study';
import { COLORS } from '../../utils/theme';
import { getApiErrorMessage } from '../../utils/apiError';

const cardTypeLabels: Record<string, string> = {
  Vocabulary: 'Từ vựng',
  Grammar: 'Cấu trúc',
  QA: 'Hỏi đáp',
};

const StudyCardsScreen = ({ navigation, route }: any) => {
  const deckId = route.params?.deckId as number | undefined;
  const deckName = route.params?.deckName as string | undefined;
  const [cards, setCards] = useState<StudyCard[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const fetchCards = async (showLoading = true) => {
    if (showLoading) setLoading(true);
    try {
      const res = await studyService.getCards(deckId);
      if (res.success && res.data) {
        setCards(res.data);
      }
    } catch (e: any) {
      Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể tải danh sách thẻ.'));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    navigation.setOptions({ title: deckName || 'Danh sách thẻ' });
    const unsubscribe = navigation.addListener('focus', () => fetchCards(false));
    fetchCards(true);
    return unsubscribe;
  }, [navigation, deckId, deckName]);

  const onRefresh = () => {
    setRefreshing(true);
    fetchCards(false);
  };

  const deleteCard = (card: StudyCard) => {
    Alert.alert('Xóa thẻ', `Bạn muốn xóa "${card.frontText}"?`, [
      { text: 'Hủy', style: 'cancel' },
      {
        text: 'Xóa',
        style: 'destructive',
        onPress: async () => {
          try {
            await studyService.deleteCard(card.id);
            fetchCards(false);
          } catch (e: any) {
            Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể xóa thẻ.'));
          }
        },
      },
    ]);
  };

  if (loading) {
    return <Loading fullScreen message="Đang tải danh sách thẻ..." />;
  }

  return (
    <PageContainer>
      <ScrollView
        contentContainerStyle={styles.content}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} colors={[COLORS.primary]} />}
      >
        <View style={styles.headerRow}>
          <View style={styles.headerText}>
            <Text style={styles.title} numberOfLines={1}>{deckName || 'Tất cả thẻ'}</Text>
            <Text style={styles.subtitle}>{cards.length} thẻ đang lưu</Text>
          </View>
          <View style={styles.headerActions}>
            {deckId ? (
              <TouchableOpacity
                style={[styles.smallButton, styles.reviewButton]}
                onPress={() => navigation.navigate('StudyReview', { deckId, deckName })}
              >
                <Text style={styles.reviewButtonText}>Ôn</Text>
              </TouchableOpacity>
            ) : null}
            <TouchableOpacity
              style={styles.smallButton}
              onPress={() => navigation.navigate('AddStudyCard', { deckId })}
            >
              <Text style={styles.smallButtonText}>+ Thẻ</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={styles.smallButton}
              onPress={() => navigation.navigate('AiVocabulary', { deckId })}
            >
              <Text style={styles.smallButtonText}>AI</Text>
            </TouchableOpacity>
          </View>
        </View>

        {cards.length > 0 ? (
          cards.map((card) => (
            <View key={card.id} style={styles.card}>
              <View style={styles.cardTop}>
                <Text style={styles.typePill}>{cardTypeLabels[card.cardType] || card.cardType}</Text>
                <TouchableOpacity style={styles.deleteButton} onPress={() => deleteCard(card)}>
                  <Text style={styles.deleteText}>Xóa</Text>
                </TouchableOpacity>
              </View>
              <Text style={styles.front}>{card.frontText}</Text>
              <Text style={styles.back}>{card.backText}</Text>
              {card.example ? <Text style={styles.example} numberOfLines={2}>{card.example}</Text> : null}
              {card.note ? <Text style={styles.note} numberOfLines={2}>{card.note}</Text> : null}
              <View style={styles.cardFooter}>
                {card.tags ? <Text style={styles.tags} numberOfLines={1}>{card.tags}</Text> : <Text style={styles.tagsMuted}>Chưa có tag</Text>}
                <Text style={styles.reviewText}>{card.reviewCount} lần ôn</Text>
              </View>
            </View>
          ))
        ) : (
          <View style={styles.emptyBox}>
            <Text style={styles.emptyText}>Bộ này chưa có thẻ nào.</Text>
          </View>
        )}
      </ScrollView>
    </PageContainer>
  );
};

const styles = StyleSheet.create({
  content: {
    padding: 16,
    paddingBottom: 96,
  },
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 12,
    gap: 10,
  },
  headerText: {
    flex: 1,
    minWidth: 0,
  },
  title: {
    fontSize: 20,
    fontWeight: '900',
    color: COLORS.text,
  },
  subtitle: {
    marginTop: 3,
    fontSize: 12,
    color: COLORS.textMuted,
    fontWeight: '700',
  },
  headerActions: {
    flexDirection: 'row',
    gap: 8,
  },
  smallButton: {
    minHeight: 40,
    borderRadius: 8,
    paddingHorizontal: 12,
    backgroundColor: COLORS.cardBg,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    justifyContent: 'center',
  },
  smallButtonText: {
    color: COLORS.text,
    fontWeight: '900',
  },
  reviewButton: {
    backgroundColor: COLORS.primary,
    borderColor: COLORS.primary,
  },
  reviewButtonText: {
    color: '#FFFFFF',
    fontWeight: '900',
  },
  card: {
    borderRadius: 8,
    backgroundColor: COLORS.cardBg,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    padding: 12,
    marginBottom: 10,
  },
  cardTop: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  typePill: {
    borderRadius: 8,
    overflow: 'hidden',
    backgroundColor: 'rgba(76, 101, 143, 0.12)',
    color: COLORS.secondary,
    fontSize: 11,
    fontWeight: '900',
    paddingHorizontal: 8,
    paddingVertical: 4,
  },
  deleteButton: {
    minHeight: 30,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: 'rgba(185, 28, 28, 0.26)',
    backgroundColor: 'rgba(185, 28, 28, 0.08)',
    paddingHorizontal: 10,
    alignItems: 'center',
    justifyContent: 'center',
  },
  deleteText: {
    color: '#B91C1C',
    fontSize: 12,
    fontWeight: '900',
  },
  front: {
    fontSize: 17,
    fontWeight: '900',
    color: COLORS.text,
  },
  back: {
    marginTop: 6,
    fontSize: 14,
    color: COLORS.text,
    lineHeight: 20,
  },
  example: {
    marginTop: 8,
    fontSize: 13,
    color: COLORS.textMuted,
    fontStyle: 'italic',
  },
  note: {
    marginTop: 8,
    fontSize: 13,
    color: COLORS.text,
    fontWeight: '700',
  },
  cardFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginTop: 10,
    gap: 8,
  },
  tags: {
    flex: 1,
    minWidth: 0,
    fontSize: 12,
    color: COLORS.primary,
    fontWeight: '800',
  },
  tagsMuted: {
    flex: 1,
    minWidth: 0,
    fontSize: 12,
    color: COLORS.textMuted,
    fontWeight: '700',
  },
  reviewText: {
    color: COLORS.textMuted,
    fontSize: 11,
    fontWeight: '700',
  },
  emptyBox: {
    borderRadius: 8,
    backgroundColor: COLORS.cardBg,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    paddingVertical: 34,
    alignItems: 'center',
  },
  emptyText: {
    color: COLORS.textMuted,
    fontWeight: '700',
  },
});

export default StudyCardsScreen;

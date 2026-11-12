import React, { useEffect, useState } from 'react';
import { Alert, Modal, RefreshControl, ScrollView, StyleSheet, Text, TextInput, TouchableOpacity, View } from 'react-native';
import PageContainer from '../../components/PageContainer';
import Loading from '../../components/Loading';
import studyService from '../../services/studyService';
import { StudyDeck, StudyStats } from '../../types/study';
import { COLORS } from '../../utils/theme';
import { getApiErrorMessage } from '../../utils/apiError';

const StudyHomeScreen = ({ navigation }: any) => {
  const [stats, setStats] = useState<StudyStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [deckModalVisible, setDeckModalVisible] = useState(false);
  const [deckName, setDeckName] = useState('');
  const [deckDescription, setDeckDescription] = useState('');
  const [deckLanguage, setDeckLanguage] = useState('english');
  const [savingDeck, setSavingDeck] = useState(false);

  const fetchData = async (showLoading = true) => {
    if (showLoading) setLoading(true);
    try {
      const res = await studyService.getStats();
      if (res.success && res.data) {
        setStats(res.data);
      }
    } catch (e: any) {
      Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể tải dữ liệu học bài.'));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    const unsubscribe = navigation.addListener('focus', () => fetchData(false));
    fetchData(true);
    return unsubscribe;
  }, [navigation]);

  const onRefresh = () => {
    setRefreshing(true);
    fetchData(false);
  };

  const closeDeckModal = () => {
    setDeckModalVisible(false);
    setDeckName('');
    setDeckDescription('');
    setDeckLanguage('english');
  };

  const createDeck = async () => {
    if (!deckName.trim()) {
      Alert.alert('Thiếu tên bộ thẻ', 'Vui lòng nhập tên bộ thẻ.');
      return;
    }

    setSavingDeck(true);
    try {
      await studyService.createDeck({
        name: deckName.trim(),
        description: deckDescription.trim() || undefined,
        language: deckLanguage,
      });
      closeDeckModal();
      fetchData(false);
    } catch (e: any) {
      Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể tạo bộ thẻ.'));
    } finally {
      setSavingDeck(false);
    }
  };

  const deleteDeck = (deck: StudyDeck) => {
    Alert.alert(
      'Xóa bộ thẻ',
      `Xóa "${deck.name}" sẽ xóa toàn bộ thẻ và lịch ôn trong bộ này.`,
      [
        { text: 'Hủy', style: 'cancel' },
        {
          text: 'Xóa',
          style: 'destructive',
          onPress: async () => {
            try {
              await studyService.deleteDeck(deck.id);
              fetchData(false);
            } catch (e: any) {
              Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể xóa bộ thẻ.'));
            }
          },
        },
      ],
    );
  };

  const renderDeck = (deck: StudyDeck) => (
    <View key={deck.id} style={styles.deckRow}>
      <TouchableOpacity
        style={styles.deckMain}
        onPress={() => navigation.navigate('StudyCards', { deckId: deck.id, deckName: deck.name })}
        activeOpacity={0.82}
      >
        <View style={styles.deckIcon}>
          <Text style={styles.deckIconText}>{deck.name.slice(0, 1).toUpperCase()}</Text>
        </View>
        <View style={styles.deckBody}>
          <Text style={styles.deckName} numberOfLines={1}>{deck.name}</Text>
          <Text style={styles.deckDesc} numberOfLines={1}>{deck.description || 'Bộ thẻ học cá nhân'}</Text>
          <Text style={styles.deckSmall}>{deck.cardCount} thẻ</Text>
        </View>
        <View style={styles.deckMeta}>
          <Text style={styles.deckDue}>{deck.dueCount}</Text>
          <Text style={styles.deckMetaLabel}>cần ôn</Text>
        </View>
      </TouchableOpacity>

      <View style={styles.deckActions}>
        <TouchableOpacity
          style={[styles.deckActionButton, styles.reviewDeckButton]}
          onPress={() => navigation.navigate('StudyReview', { deckId: deck.id, deckName: deck.name })}
        >
          <Text style={styles.reviewDeckText}>Ôn</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={styles.deckActionButton}
          onPress={() => navigation.navigate('StudyCards', { deckId: deck.id, deckName: deck.name })}
        >
          <Text style={styles.deckActionText}>Thẻ</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.deckActionButton, styles.deleteDeckButton]}
          onPress={() => deleteDeck(deck)}
        >
          <Text style={styles.deleteDeckText}>Xóa</Text>
        </TouchableOpacity>
      </View>
    </View>
  );

  if (loading) {
    return <Loading fullScreen message="Đang tải thẻ học..." />;
  }

  return (
    <PageContainer>
      <ScrollView
        contentContainerStyle={styles.content}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} colors={[COLORS.primary]} />}
      >
        <View style={styles.hero}>
          <View style={styles.heroText}>
            <Text style={styles.heroLabel}>Ôn hôm nay</Text>
            <Text style={styles.heroNumber}>{stats?.dueToday ?? 0}</Text>
            <Text style={styles.heroSub}>thẻ đang tới hạn trong tất cả bộ</Text>
          </View>
          <TouchableOpacity style={styles.reviewButton} onPress={() => navigation.navigate('StudyReview')}>
            <Text style={styles.reviewButtonText}>Chọn bộ ôn</Text>
          </TouchableOpacity>
        </View>

        <View style={styles.statGrid}>
          <View style={styles.statBox}>
            <Text style={styles.statValue}>{stats?.reviewedToday ?? 0}</Text>
            <Text style={styles.statLabel}>Đã ôn</Text>
          </View>
          <View style={styles.statBox}>
            <Text style={styles.statValue}>{stats?.totalCards ?? 0}</Text>
            <Text style={styles.statLabel}>Tổng thẻ</Text>
          </View>
          <View style={styles.statBox}>
            <Text style={styles.statValue}>{stats?.streakDays ?? 0}</Text>
            <Text style={styles.statLabel}>Streak</Text>
          </View>
        </View>

        <View style={styles.actionRow}>
          <TouchableOpacity style={styles.actionButton} onPress={() => setDeckModalVisible(true)}>
            <Text style={styles.actionText}>Thêm bộ</Text>
          </TouchableOpacity>
          <TouchableOpacity style={styles.actionButton} onPress={() => navigation.navigate('AddStudyCard')}>
            <Text style={styles.actionText}>Thêm thẻ</Text>
          </TouchableOpacity>
          <TouchableOpacity style={styles.actionButton} onPress={() => navigation.navigate('AiVocabulary')}>
            <Text style={styles.actionText}>AI từ vựng</Text>
          </TouchableOpacity>
          <TouchableOpacity style={styles.actionButton} onPress={() => navigation.navigate('StudyStats')}>
            <Text style={styles.actionText}>Thống kê</Text>
          </TouchableOpacity>
        </View>

        <View style={styles.sectionHeader}>
          <Text style={styles.sectionTitle}>Bộ thẻ</Text>
          <Text style={styles.sectionCount}>{stats?.decks?.length ?? 0}</Text>
        </View>

        <View style={styles.deckList}>
          {stats?.decks && stats.decks.length > 0 ? (
            stats.decks.map(renderDeck)
          ) : (
            <View style={styles.emptyBox}>
              <Text style={styles.emptyText}>Chưa có bộ thẻ nào.</Text>
            </View>
          )}
        </View>
      </ScrollView>

      <Modal visible={deckModalVisible} transparent animationType="fade" onRequestClose={closeDeckModal}>
        <View style={styles.modalOverlay}>
          <View style={styles.modalCard}>
            <Text style={styles.modalTitle}>Thêm bộ thẻ</Text>
            <TextInput
              style={styles.input}
              value={deckName}
              onChangeText={setDeckName}
              placeholder="English Interview"
              placeholderTextColor={COLORS.textMuted}
            />
            <TextInput
              style={[styles.input, styles.multiInput]}
              value={deckDescription}
              onChangeText={setDeckDescription}
              placeholder="Mô tả ngắn"
              placeholderTextColor={COLORS.textMuted}
              multiline
            />
            <Text style={styles.modalLabel}>Ngôn ngữ</Text>
            <View style={styles.languageRow}>
              {[
                { value: 'english', label: 'English' },
                { value: 'japanese', label: 'Japanese' },
                { value: 'other', label: 'Other' },
              ].map((item) => (
                <TouchableOpacity
                  key={item.value}
                  style={[styles.languageChip, deckLanguage === item.value && styles.languageChipActive]}
                  onPress={() => setDeckLanguage(item.value)}
                >
                  <Text style={[styles.languageChipText, deckLanguage === item.value && styles.languageChipTextActive]}>
                    {item.label}
                  </Text>
                </TouchableOpacity>
              ))}
            </View>
            <View style={styles.modalActions}>
              <TouchableOpacity style={styles.cancelButton} onPress={closeDeckModal}>
                <Text style={styles.cancelText}>Hủy</Text>
              </TouchableOpacity>
              <TouchableOpacity style={styles.saveButton} onPress={createDeck} disabled={savingDeck}>
                <Text style={styles.saveText}>{savingDeck ? 'Đang lưu...' : 'Lưu'}</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>
    </PageContainer>
  );
};

const styles = StyleSheet.create({
  content: {
    padding: 16,
    paddingBottom: 96,
  },
  hero: {
    minHeight: 148,
    borderRadius: 8,
    backgroundColor: COLORS.cardBg,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    padding: 18,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 12,
  },
  heroText: {
    flex: 1,
    minWidth: 0,
  },
  heroLabel: {
    fontSize: 14,
    fontWeight: '800',
    color: COLORS.primary,
  },
  heroNumber: {
    fontSize: 48,
    fontWeight: '900',
    color: COLORS.primaryDark,
    marginTop: 4,
  },
  heroSub: {
    fontSize: 13,
    color: COLORS.textMuted,
    fontWeight: '600',
  },
  reviewButton: {
    minHeight: 46,
    borderRadius: 8,
    backgroundColor: COLORS.primary,
    paddingHorizontal: 14,
    alignItems: 'center',
    justifyContent: 'center',
  },
  reviewButtonText: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '800',
  },
  statGrid: {
    flexDirection: 'row',
    gap: 8,
    marginTop: 10,
  },
  statBox: {
    flex: 1,
    minHeight: 78,
    borderRadius: 8,
    backgroundColor: COLORS.cardBg,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    alignItems: 'center',
    justifyContent: 'center',
  },
  statValue: {
    fontSize: 22,
    fontWeight: '900',
    color: COLORS.text,
  },
  statLabel: {
    fontSize: 12,
    color: COLORS.textMuted,
    marginTop: 4,
    fontWeight: '700',
  },
  actionRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginTop: 10,
  },
  actionButton: {
    width: '48.7%',
    minHeight: 44,
    borderRadius: 8,
    backgroundColor: COLORS.cardBgLight,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    alignItems: 'center',
    justifyContent: 'center',
  },
  actionText: {
    fontSize: 13,
    fontWeight: '800',
    color: COLORS.text,
  },
  sectionHeader: {
    marginTop: 20,
    marginBottom: 8,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  sectionTitle: {
    fontSize: 17,
    fontWeight: '900',
    color: COLORS.text,
  },
  sectionCount: {
    minWidth: 32,
    textAlign: 'center',
    borderRadius: 8,
    overflow: 'hidden',
    paddingVertical: 5,
    paddingHorizontal: 8,
    backgroundColor: COLORS.cardBgLight,
    color: COLORS.primary,
    fontWeight: '900',
  },
  deckList: {
    borderRadius: 8,
    backgroundColor: COLORS.cardBg,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    padding: 8,
  },
  deckRow: {
    borderRadius: 8,
    padding: 8,
    marginBottom: 8,
    backgroundColor: 'rgba(255, 255, 255, 0.62)',
  },
  deckMain: {
    minHeight: 66,
    flexDirection: 'row',
    alignItems: 'center',
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
    fontWeight: '600',
  },
  deckSmall: {
    fontSize: 11,
    color: COLORS.secondary,
    marginTop: 3,
    fontWeight: '800',
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
  deckActions: {
    flexDirection: 'row',
    gap: 8,
    marginTop: 8,
  },
  deckActionButton: {
    flex: 1,
    minHeight: 36,
    borderRadius: 8,
    backgroundColor: COLORS.cardBgLight,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    alignItems: 'center',
    justifyContent: 'center',
  },
  reviewDeckButton: {
    backgroundColor: COLORS.primary,
    borderColor: COLORS.primary,
  },
  deleteDeckButton: {
    backgroundColor: 'rgba(185, 28, 28, 0.08)',
    borderColor: 'rgba(185, 28, 28, 0.26)',
  },
  deckActionText: {
    color: COLORS.text,
    fontSize: 12,
    fontWeight: '900',
  },
  reviewDeckText: {
    color: '#FFFFFF',
    fontSize: 12,
    fontWeight: '900',
  },
  deleteDeckText: {
    color: '#B91C1C',
    fontSize: 12,
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
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(45, 35, 39, 0.48)',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 18,
  },
  modalCard: {
    width: '100%',
    maxWidth: 420,
    borderRadius: 8,
    backgroundColor: COLORS.cardBg,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    padding: 16,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: '900',
    color: COLORS.text,
    marginBottom: 12,
  },
  input: {
    minHeight: 46,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: COLORS.border,
    backgroundColor: 'rgba(255, 255, 255, 0.86)',
    paddingHorizontal: 12,
    paddingVertical: 10,
    color: COLORS.text,
    fontSize: 14,
    fontWeight: '600',
    marginBottom: 10,
  },
  multiInput: {
    minHeight: 78,
    textAlignVertical: 'top',
  },
  modalLabel: {
    fontSize: 12,
    fontWeight: '900',
    color: COLORS.text,
    marginBottom: 7,
  },
  languageRow: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 12,
  },
  languageChip: {
    flex: 1,
    minHeight: 38,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    backgroundColor: COLORS.cardBgLight,
    alignItems: 'center',
    justifyContent: 'center',
  },
  languageChipActive: {
    backgroundColor: COLORS.primary,
    borderColor: COLORS.primary,
  },
  languageChipText: {
    color: COLORS.text,
    fontSize: 12,
    fontWeight: '900',
  },
  languageChipTextActive: {
    color: '#FFFFFF',
  },
  modalActions: {
    flexDirection: 'row',
    gap: 8,
    marginTop: 4,
  },
  cancelButton: {
    flex: 1,
    minHeight: 44,
    borderRadius: 8,
    backgroundColor: COLORS.cardBgLight,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    alignItems: 'center',
    justifyContent: 'center',
  },
  saveButton: {
    flex: 1,
    minHeight: 44,
    borderRadius: 8,
    backgroundColor: COLORS.primary,
    alignItems: 'center',
    justifyContent: 'center',
  },
  cancelText: {
    color: COLORS.text,
    fontWeight: '900',
  },
  saveText: {
    color: '#FFFFFF',
    fontWeight: '900',
  },
});

export default StudyHomeScreen;

import React, { useEffect, useState } from 'react';
import { Alert, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import PageContainer from '../../components/PageContainer';
import Loading from '../../components/Loading';
import studyService from '../../services/studyService';
import { StudyStats } from '../../types/study';
import { COLORS } from '../../utils/theme';
import { getApiErrorMessage } from '../../utils/apiError';

const StudyStatsScreen = () => {
  const [stats, setStats] = useState<StudyStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const fetchStats = async (showLoading = true) => {
    if (showLoading) setLoading(true);
    try {
      const res = await studyService.getStats();
      if (res.success && res.data) {
        setStats(res.data);
      }
    } catch (e: any) {
      Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể tải thống kê học bài.'));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    fetchStats(true);
  }, []);

  const onRefresh = () => {
    setRefreshing(true);
    fetchStats(false);
  };

  if (loading) {
    return <Loading fullScreen message="Đang tải thống kê..." />;
  }

  return (
    <PageContainer>
      <ScrollView
        contentContainerStyle={styles.content}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} colors={[COLORS.primary]} />}
      >
        <View style={styles.grid}>
          <View style={styles.statCard}>
            <Text style={styles.value}>{stats?.reviewedToday ?? 0}</Text>
            <Text style={styles.label}>Hôm nay đã ôn</Text>
          </View>
          <View style={styles.statCard}>
            <Text style={styles.value}>{stats?.totalCards ?? 0}</Text>
            <Text style={styles.label}>Tổng số card</Text>
          </View>
          <View style={styles.statCard}>
            <Text style={styles.value}>{stats?.weakCards ?? 0}</Text>
            <Text style={styles.label}>Card yếu</Text>
          </View>
          <View style={styles.statCard}>
            <Text style={styles.value}>{stats?.upcomingCards ?? 0}</Text>
            <Text style={styles.label}>Card sắp quên</Text>
          </View>
        </View>

        <Text style={styles.sectionTitle}>Theo bộ thẻ</Text>
        <View style={styles.deckList}>
          {stats?.decks.map((deck) => (
            <View key={deck.id} style={styles.deckRow}>
              <View style={styles.deckText}>
                <Text style={styles.deckName}>{deck.name}</Text>
                <Text style={styles.deckMeta}>{deck.cardCount} thẻ</Text>
              </View>
              <Text style={styles.deckDue}>{deck.dueCount} cần ôn</Text>
            </View>
          ))}
        </View>
      </ScrollView>
    </PageContainer>
  );
};

const styles = StyleSheet.create({
  content: {
    padding: 16,
    paddingBottom: 96,
  },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
  },
  statCard: {
    width: '48.5%',
    minHeight: 112,
    borderRadius: 8,
    backgroundColor: COLORS.cardBg,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    padding: 14,
    justifyContent: 'center',
  },
  value: {
    fontSize: 30,
    fontWeight: '900',
    color: COLORS.primaryDark,
  },
  label: {
    marginTop: 7,
    fontSize: 13,
    color: COLORS.textMuted,
    fontWeight: '800',
  },
  sectionTitle: {
    marginTop: 22,
    marginBottom: 10,
    fontSize: 17,
    fontWeight: '900',
    color: COLORS.text,
  },
  deckList: {
    borderRadius: 8,
    backgroundColor: COLORS.cardBg,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    padding: 8,
  },
  deckRow: {
    minHeight: 60,
    borderRadius: 8,
    backgroundColor: 'rgba(255, 255, 255, 0.62)',
    paddingHorizontal: 10,
    marginBottom: 7,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  deckText: {
    flex: 1,
    minWidth: 0,
    marginRight: 10,
  },
  deckName: {
    fontSize: 14,
    fontWeight: '900',
    color: COLORS.text,
  },
  deckMeta: {
    marginTop: 3,
    fontSize: 12,
    color: COLORS.textMuted,
    fontWeight: '700',
  },
  deckDue: {
    color: COLORS.primary,
    fontWeight: '900',
    fontSize: 12,
  },
});

export default StudyStatsScreen;

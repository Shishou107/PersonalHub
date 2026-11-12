import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, RefreshControl, TouchableOpacity, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import reportService from '../../services/reportService';
import { MonthlyExpenseReport } from '../../types/report';
import AppCard from '../../components/AppCard';
import Loading from '../../components/Loading';
import { formatMoney } from '../../utils/formatMoney';
import PageContainer from '../../components/PageContainer';
import { COLORS } from '../../utils/theme';
import { getApiErrorMessage } from '../../utils/apiError';

const ReportScreen = ({ navigation }: any) => {
  const [report, setReport] = useState<MonthlyExpenseReport | null>(null);
  const [month, setMonth] = useState(new Date().getMonth() + 1);
  const [year, setYear] = useState(new Date().getFullYear());
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const fetchReport = async (showLoading = true) => {
    if (showLoading) setLoading(true);
    try {
      const res = await reportService.getMonthlyReport(month, year);
      if (res.success && res.data) {
        setReport(res.data);
      }
    } catch (e: any) {
      console.error('Lỗi khi tải báo cáo chi tiêu:', e);
      Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể tải báo cáo chi tiêu.'));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    fetchReport(true);
  }, [month, year]);

  useEffect(() => {
    const unsubscribe = navigation.addListener('focus', () => {
      fetchReport(false);
    });
    return unsubscribe;
  }, [navigation, month, year]);

  const onRefresh = () => {
    setRefreshing(true);
    fetchReport(false);
  };

  const handlePrevMonth = () => {
    if (month === 1) {
      setMonth(12);
      setYear((y) => y - 1);
    } else {
      setMonth((m) => m - 1);
    }
  };

  const handleNextMonth = () => {
    if (month === 12) {
      setMonth(1);
      setYear((y) => y + 1);
    } else {
      setMonth((m) => m + 1);
    }
  };

  if (loading) {
    return <Loading fullScreen message="Đang kết xuất báo cáo..." />;
  }

  return (
    <PageContainer>
      <ScrollView
        contentContainerStyle={styles.scrollContainer}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} colors={[COLORS.primary]} tintColor={COLORS.primary} />}
      >
        {/* Month Year Selector */}
        <View style={styles.selectorRow}>
          <TouchableOpacity style={styles.navBtn} onPress={handlePrevMonth}>
            <Ionicons name="chevron-back" size={20} color={COLORS.text} />
          </TouchableOpacity>
          <Text style={styles.selectorTitle}>Tháng {month} / {year}</Text>
          <TouchableOpacity style={styles.navBtn} onPress={handleNextMonth}>
            <Ionicons name="chevron-forward" size={20} color={COLORS.text} />
          </TouchableOpacity>
        </View>

        {/* Monthly Total Expense */}
        <AppCard style={styles.totalCard}>
          <Text style={styles.totalLabel}>TỔNG CHI TIÊU THÁNG</Text>
          <Text style={styles.totalValue}>{formatMoney(report?.totalExpense || 0)}</Text>
        </AppCard>

        {/* AI Summary Highlight */}
        {report?.aiSummary ? (
          <AppCard style={styles.aiCard}>
            <View style={styles.aiHeader}>
              <Text style={styles.aiEmoji}>🤖</Text>
              <Text style={styles.aiTitle}>Nhận xét của AI:</Text>
            </View>
            <Text style={styles.aiBody}>{report.aiSummary}</Text>
          </AppCard>
        ) : null}

      {/* Category Breakdown Bar Chart */}
      <Text style={styles.sectionTitle}>Phân tích theo danh mục</Text>
      <View style={styles.breakdownContainer}>
        {report?.byCategory && report.byCategory.length > 0 ? (
          report.byCategory.map((cat, idx) => {
            const catColor = cat.color || '#6B7280';
            return (
              <View key={idx} style={styles.breakdownRow}>
                <View style={styles.breakdownHeader}>
                  <Text style={styles.breakdownName}>{cat.categoryName}</Text>
                  <Text style={styles.breakdownAmount}>{formatMoney(cat.totalAmount)}</Text>
                </View>
                
                {/* Horizontal Progress Bar */}
                <View style={styles.barContainer}>
                  <View style={[styles.barValue, { width: `${cat.percent}%`, backgroundColor: catColor }]} />
                </View>
                
                <Text style={styles.breakdownPercent}>{cat.percent}% tổng chi tiêu</Text>
              </View>
            );
          })
        ) : (
          <View style={styles.emptyContainer}>
            <Text style={styles.emptyText}>Tháng này bạn chưa ghi chép khoản chi nào.</Text>
          </View>
        )}
      </View>
      
      <View style={{ height: 32 }} />
      </ScrollView>
    </PageContainer>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: 'transparent',
  },
  scrollContainer: {
    padding: 16,
    paddingBottom: 32,
  },
  selectorRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 16,
    backgroundColor: COLORS.cardBg,
    borderRadius: 12,
    padding: 8,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
  },
  navBtn: {
    width: 40,
    height: 40,
    borderRadius: 8,
    backgroundColor: COLORS.cardBgLight,
    justifyContent: 'center',
    alignItems: 'center',
  },
  navBtnText: {
    fontSize: 14,
    color: COLORS.text,
  },
  selectorTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: COLORS.text,
    fontFamily: 'System',
  },
  totalCard: {
    alignItems: 'center',
    paddingVertical: 20,
    backgroundColor: COLORS.cardBg,
  },
  totalLabel: {
    fontSize: 12,
    fontWeight: '600',
    color: COLORS.textMuted,
    letterSpacing: 0.5,
    marginBottom: 6,
    fontFamily: 'System',
  },
  totalValue: {
    fontSize: 26,
    fontWeight: '800',
    color: COLORS.primary,
    fontFamily: 'System',
  },
  aiCard: {
    backgroundColor: COLORS.cardBgLight,
    borderColor: COLORS.border,
    borderWidth: 1,
    padding: 16,
  },
  aiHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 8,
  },
  aiEmoji: {
    fontSize: 20,
    marginRight: 8,
  },
  aiTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: COLORS.primary,
    fontFamily: 'System',
  },
  aiBody: {
    fontSize: 13,
    color: COLORS.text,
    lineHeight: 20,
    fontFamily: 'System',
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: COLORS.text,
    marginVertical: 14,
    fontFamily: 'System',
  },
  breakdownContainer: {
    backgroundColor: COLORS.cardBg,
    borderRadius: 12,
    padding: 16,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.03,
    shadowRadius: 4,
    elevation: 2,
  },
  breakdownRow: {
    marginBottom: 16,
  },
  breakdownHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 6,
  },
  breakdownName: {
    fontSize: 14,
    fontWeight: '600',
    color: COLORS.text,
    fontFamily: 'System',
  },
  breakdownAmount: {
    fontSize: 14,
    fontWeight: '700',
    color: COLORS.text,
    fontFamily: 'System',
  },
  barContainer: {
    height: 8,
    backgroundColor: COLORS.cardBgLight,
    borderRadius: 4,
    width: '100%',
    overflow: 'hidden',
    marginBottom: 6,
  },
  barValue: {
    height: '100%',
    borderRadius: 4,
  },
  breakdownPercent: {
    fontSize: 11,
    color: COLORS.textMuted,
    fontFamily: 'System',
  },
  emptyContainer: {
    paddingVertical: 32,
    alignItems: 'center',
    justifyContent: 'center',
  },
  emptyText: {
    fontSize: 14,
    color: COLORS.textMuted,
    fontFamily: 'System',
    textAlign: 'center',
  },
});

export default ReportScreen;

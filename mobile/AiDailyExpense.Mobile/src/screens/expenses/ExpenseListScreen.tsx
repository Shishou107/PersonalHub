import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, FlatList, RefreshControl, TouchableOpacity, Alert, Modal, ScrollView } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import expenseService from '../../services/expenseService';
import { Expense } from '../../types/expense';
import ExpenseItem from '../../components/ExpenseItem';
import Loading from '../../components/Loading';
import { formatMoney } from '../../utils/formatMoney';
import { formatDate } from '../../utils/formatDate';
import PageContainer from '../../components/PageContainer';
import { COLORS } from '../../utils/theme';

const ExpenseListScreen = ({ navigation }: any) => {
  const [expenses, setExpenses] = useState<Expense[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [month, setMonth] = useState(new Date().getMonth() + 1);
  const [year, setYear] = useState(new Date().getFullYear());
  const [selectedDay, setSelectedDay] = useState('Tất cả');
  const [selectedExpense, setSelectedExpense] = useState<Expense | null>(null);

  const fetchExpenses = async (showLoading = true) => {
    if (showLoading) setLoading(true);
    try {
      const pad = (n: number) => String(n).padStart(2, '0');
      const firstDay = `${year}-${pad(month)}-01`;
      const lastDay = `${year}-${pad(month)}-${pad(new Date(year, month, 0).getDate())}`;

      const res = await expenseService.getExpenses(firstDay, lastDay);
      if (res.success && res.data) {
        setExpenses(res.data);
      }
    } catch (e: any) {
      console.error('Lỗi khi tải danh sách chi tiêu:', e);
      Alert.alert('Lỗi', 'Không thể tải danh sách chi tiêu.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    const unsubscribe = navigation.addListener('focus', () => {
      fetchExpenses(false);
    });
    fetchExpenses(true);
    return unsubscribe;
  }, [navigation, month, year]);

  useEffect(() => {
    setSelectedDay('Tất cả');
  }, [month, year]);

  const onRefresh = () => {
    setRefreshing(true);
    fetchExpenses(false);
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

  const handleDeleteExpense = (id: number, title: string) => {
    Alert.alert('Xóa khoản ghi chép', `Bạn có chắc chắn muốn xóa "${title}"?`, [
      { text: 'Hủy', style: 'cancel' },
      {
        text: 'Xóa',
        style: 'destructive',
        onPress: async () => {
          try {
            const res = await expenseService.deleteExpense(id);
            if (res.success) {
              fetchExpenses(false);
            }
          } catch (e: any) {
            Alert.alert('Lỗi', e.message || 'Không thể xóa khoản ghi chép.');
          }
        },
      },
    ]);
  };

  const filteredExpenses = expenses.filter((item) => {
    if (selectedDay === 'Tất cả') return true;
    const spentDate = new Date(item.spentAt);
    const dayStr = String(spentDate.getDate()).padStart(2, '0');
    return dayStr === selectedDay;
  });

  const totalExpense = filteredExpenses
    .filter((e) => e.categoryType !== 'Income')
    .reduce((acc, curr) => acc + curr.amount, 0);

  const totalIncome = filteredExpenses
    .filter((e) => e.categoryType === 'Income')
    .reduce((acc, curr) => acc + curr.amount, 0);

  const netBalance = totalIncome - totalExpense;

  if (loading) {
    return <Loading fullScreen message="Đang tải danh sách chi tiêu..." />;
  }

  return (
    <PageContainer>
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

      {/* Multi-info Balance Card */}
      <View style={styles.summaryCard}>
        <View style={styles.summaryHeader}>
          <Text style={styles.summaryLabel}>
            {selectedDay === 'Tất cả' ? 'Số dư thu chi tháng này' : `Số dư ngày ${selectedDay}/${month}`}
          </Text>
          <Text style={[
            styles.summaryValue,
            { color: netBalance >= 0 ? '#059669' : '#DC2626' }
          ]}>
            {netBalance >= 0 ? '+' : ''}{formatMoney(netBalance)}
          </Text>
        </View>
        <View style={styles.summaryDivider} />
        <View style={styles.summaryRow}>
          <View style={styles.summaryItem}>
            <Text style={[styles.summaryItemLabel, styles.incomeLabel]}>Tổng Thu nhập</Text>
            <Text style={[styles.summaryItemValue, styles.incomeValue]}>
              +{formatMoney(totalIncome)}
            </Text>
          </View>
          <View style={styles.summaryItem}>
            <Text style={[styles.summaryItemLabel, styles.expenseLabel]}>Tổng Chi tiêu</Text>
            <Text style={[styles.summaryItemValue, styles.expenseValue]}>
              -{formatMoney(totalExpense)}
            </Text>
          </View>
        </View>
      </View>

      {/* Horizontal Day Selector */}
      <View style={styles.daySelectorWrapper}>
        <FlatList
          horizontal
          data={['Tất cả', ...Array.from({ length: new Date(year, month, 0).getDate() }, (_, i) => String(i + 1).padStart(2, '0'))]}
          keyExtractor={(item) => item}
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={styles.daySelectorContent}
          renderItem={({ item }) => {
            const isSelected = selectedDay === item;
            return (
              <TouchableOpacity
                style={[styles.dayButton, isSelected && styles.dayButtonSelected]}
                onPress={() => setSelectedDay(item)}
                activeOpacity={0.8}
              >
                <Text style={[styles.dayButtonText, isSelected && styles.dayButtonTextSelected]}>
                  {item === 'Tất cả' ? 'Tất cả' : `Ngày ${item}`}
                </Text>
              </TouchableOpacity>
            );
          }}
        />
      </View>

      <FlatList
        data={filteredExpenses}
        keyExtractor={(item) => String(item.expenseId)}
        renderItem={({ item }) => (
          <TouchableOpacity
            onPress={() => setSelectedExpense(item)}
            onLongPress={() => handleDeleteExpense(item.expenseId, item.title)}
            activeOpacity={0.72}
          >
            <ExpenseItem item={item} />
          </TouchableOpacity>
        )}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} colors={[COLORS.primary]} tintColor={COLORS.primary} />}
        contentContainerStyle={styles.listContainer}
        ListEmptyComponent={
          <View style={styles.emptyContainer}>
            <Text style={styles.emptyText}>Chưa có giao dịch nào được ghi nhận trong thời gian này.</Text>
            <Text style={styles.emptySubText}>Tạo mới bằng cách nhấn nút "+" phía dưới.</Text>
          </View>
        }
      />

      <TouchableOpacity
        style={styles.fab}
        onPress={() => navigation.navigate('AddExpense')}
        activeOpacity={0.85}
      >
        <Text style={styles.fabText}>+</Text>
      </TouchableOpacity>

      {/* Detail Modal */}
      <Modal
        visible={selectedExpense !== null}
        transparent
        animationType="slide"
        onRequestClose={() => setSelectedExpense(null)}
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalCard}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>Chi tiết giao dịch</Text>
              <TouchableOpacity onPress={() => setSelectedExpense(null)}>
                <Ionicons name="close" size={24} color="#64748B" />
              </TouchableOpacity>
            </View>

            {selectedExpense && (
              <ScrollView contentContainerStyle={styles.modalBody} showsVerticalScrollIndicator={false}>
                <View style={[
                  styles.amountBox,
                  { backgroundColor: selectedExpense.categoryType === 'Income' ? '#ECFDF5' : '#FEF2F2' }
                ]}>
                  <Text style={[
                    styles.detailAmount,
                    { color: selectedExpense.categoryType === 'Income' ? '#059669' : '#DC2626' }
                  ]}>
                    {selectedExpense.categoryType === 'Income' ? '+' : '-'}{formatMoney(selectedExpense.amount)}
                  </Text>
                  <Text style={styles.detailTitle}>{selectedExpense.title}</Text>
                </View>

                <View style={styles.infoGrid}>
                  <View style={styles.infoRow}>
                    <Text style={styles.infoLabel}>Loại giao dịch:</Text>
                    <Text style={[
                      styles.infoValue,
                      { color: selectedExpense.categoryType === 'Income' ? '#059669' : '#DC2626' }
                    ]}>
                      {selectedExpense.categoryType === 'Income' ? 'Thu nhập' : 'Chi tiêu'}
                    </Text>
                  </View>

                  <View style={styles.infoRow}>
                    <Text style={styles.infoLabel}>Danh mục:</Text>
                    <View style={styles.categoryBadgeRow}>
                      <View style={[styles.colorDot, { backgroundColor: selectedExpense.categoryColor || '#64748B' }]} />
                      <Text style={styles.infoValue}>{selectedExpense.categoryName || 'Khác'}</Text>
                    </View>
                  </View>

                  <View style={styles.infoRow}>
                    <Text style={styles.infoLabel}>Thời gian:</Text>
                    <Text style={styles.infoValue}>{formatDate(selectedExpense.spentAt)}</Text>
                  </View>

                  <View style={styles.infoRow}>
                    <Text style={styles.infoLabel}>Phương thức:</Text>
                    <Text style={styles.infoValue}>
                      {selectedExpense.paymentMethod === 'Cash' ? '💵 Tiền mặt' : '💳 Chuyển khoản'}
                    </Text>
                  </View>
                </View>

                {selectedExpense.note ? (
                  <View style={styles.noteSection}>
                    <Text style={styles.sectionLabel}>Ghi chú:</Text>
                    <Text style={styles.noteText}>{selectedExpense.note}</Text>
                  </View>
                ) : null}

                {selectedExpense.rawText ? (
                  <View style={styles.aiRawSection}>
                    <Text style={styles.aiRawLabel}>🤖 Nội dung AI phân tích gốc:</Text>
                    <Text style={styles.aiRawText}>{selectedExpense.rawText}</Text>
                  </View>
                ) : null}
              </ScrollView>
            )}

            <TouchableOpacity
              style={styles.closeButton}
              onPress={() => setSelectedExpense(null)}
            >
              <Text style={styles.closeButtonText}>Đóng</Text>
            </TouchableOpacity>
          </View>
        </View>
      </Modal>
    </PageContainer>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: 'transparent',
  },
  selectorRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginHorizontal: 16,
    marginTop: 12,
    marginBottom: 8,
    backgroundColor: COLORS.cardBg,
    borderRadius: 10,
    padding: 8,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
  },
  navBtn: {
    width: 36,
    height: 36,
    borderRadius: 6,
    backgroundColor: COLORS.cardBgLight,
    justifyContent: 'center',
    alignItems: 'center',
  },
  navBtnText: {
    fontSize: 12,
    color: COLORS.text,
  },
  selectorTitle: {
    fontSize: 15,
    fontWeight: '700',
    color: COLORS.text,
    fontFamily: 'System',
  },
  summaryCard: {
    backgroundColor: COLORS.cardBg,
    marginHorizontal: 16,
    marginBottom: 12,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    padding: 14,
  },
  summaryHeader: {
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 8,
  },
  summaryLabel: {
    fontSize: 12,
    fontWeight: '600',
    color: COLORS.textMuted,
    marginBottom: 4,
  },
  summaryValue: {
    fontSize: 22,
    fontWeight: '800',
  },
  summaryDivider: {
    height: 1,
    backgroundColor: COLORS.cardBorder,
    marginVertical: 6,
  },
  summaryRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  summaryItem: {
    flex: 1,
    alignItems: 'center',
  },
  summaryItemLabel: {
    fontSize: 11,
    fontWeight: '600',
    marginBottom: 2,
  },
  incomeLabel: {
    color: '#047857',
  },
  expenseLabel: {
    color: '#B91C1C',
  },
  summaryItemValue: {
    fontSize: 14,
    fontWeight: '700',
  },
  incomeValue: {
    color: '#059669',
  },
  expenseValue: {
    color: '#DC2626',
  },
  daySelectorWrapper: {
    marginBottom: 8,
    borderBottomWidth: 1,
    borderBottomColor: COLORS.cardBorder,
    paddingBottom: 8,
  },
  daySelectorContent: {
    paddingHorizontal: 16,
    gap: 6,
  },
  dayButton: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 16,
    backgroundColor: COLORS.cardBgLight,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    marginRight: 6,
  },
  dayButtonSelected: {
    backgroundColor: COLORS.primary,
    borderColor: COLORS.primary,
  },
  dayButtonText: {
    fontSize: 12,
    fontWeight: '600',
    color: COLORS.text,
  },
  dayButtonTextSelected: {
    color: '#FFFFFF',
  },
  listContainer: {
    padding: 16,
    paddingBottom: 88,
  },
  emptyContainer: {
    paddingVertical: 48,
    alignItems: 'center',
  },
  emptyText: {
    fontSize: 14,
    fontWeight: '600',
    color: COLORS.text,
    fontFamily: 'System',
    textAlign: 'center',
    marginBottom: 8,
  },
  emptySubText: {
    fontSize: 12,
    color: COLORS.textMuted,
    fontFamily: 'System',
    textAlign: 'center',
  },
  fab: {
    position: 'absolute',
    bottom: 24,
    right: 24,
    width: 54,
    height: 54,
    borderRadius: 27,
    backgroundColor: COLORS.primary,
    justifyContent: 'center',
    alignItems: 'center',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.14,
    shadowRadius: 8,
    elevation: 4,
  },
  fabText: {
    color: '#FFFFFF',
    fontSize: 28,
    fontWeight: '700',
    fontFamily: 'System',
    lineHeight: 31,
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(15, 23, 42, 0.5)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
  },
  modalCard: {
    width: '100%',
    maxWidth: 400,
    backgroundColor: 'rgba(255, 255, 255, 0.95)',
    borderRadius: 12,
    padding: 16,
    maxHeight: '85%',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.15,
    shadowRadius: 10,
    elevation: 5,
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    borderBottomWidth: 1,
    borderBottomColor: COLORS.cardBorder,
    paddingBottom: 10,
    marginBottom: 12,
  },
  modalTitle: {
    fontSize: 16,
    fontWeight: '800',
    color: COLORS.text,
  },
  modalBody: {
    paddingBottom: 10,
  },
  amountBox: {
    borderRadius: 10,
    padding: 16,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 16,
  },
  detailAmount: {
    fontSize: 24,
    fontWeight: '900',
    marginBottom: 6,
  },
  detailTitle: {
    fontSize: 15,
    fontWeight: '700',
    color: COLORS.text,
    textAlign: 'center',
  },
  infoGrid: {
    backgroundColor: COLORS.cardBgLight,
    borderRadius: 8,
    padding: 12,
    marginBottom: 16,
    gap: 10,
  },
  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  infoLabel: {
    fontSize: 13,
    color: COLORS.textMuted,
    fontWeight: '600',
  },
  infoValue: {
    fontSize: 13,
    color: COLORS.text,
    fontWeight: '700',
  },
  categoryBadgeRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  colorDot: {
    width: 10,
    height: 10,
    borderRadius: 5,
  },
  noteSection: {
    marginBottom: 16,
    paddingHorizontal: 4,
  },
  sectionLabel: {
    fontSize: 13,
    color: COLORS.textMuted,
    fontWeight: '700',
    marginBottom: 4,
  },
  noteText: {
    fontSize: 13,
    color: COLORS.text,
    lineHeight: 18,
  },
  aiRawSection: {
    backgroundColor: COLORS.cardBgLight,
    borderColor: COLORS.border,
    borderWidth: 1,
    borderRadius: 8,
    padding: 10,
    marginBottom: 8,
  },
  aiRawLabel: {
    fontSize: 12,
    fontWeight: '700',
    color: COLORS.primary,
    marginBottom: 4,
  },
  aiRawText: {
    fontSize: 12,
    color: COLORS.text,
    lineHeight: 16,
  },
  closeButton: {
    backgroundColor: COLORS.cardBgLight,
    height: 40,
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 10,
  },
  closeButtonText: {
    fontSize: 14,
    fontWeight: '700',
    color: COLORS.text,
  },
});

export default ExpenseListScreen;

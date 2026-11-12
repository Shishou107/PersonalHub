import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, Alert, TouchableOpacity } from 'react-native';
import expenseService from '../../services/expenseService';
import categoryService from '../../services/categoryService';
import { Category } from '../../types/category';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import AppInput from '../../components/AppInput';
import AppButton from '../../components/AppButton';
import AppCard from '../../components/AppCard';
import PageContainer from '../../components/PageContainer';
import { COLORS } from '../../utils/theme';

const AddExpenseScreen = ({ navigation }: any) => {
  const [title, setTitle] = useState('');
  const [amount, setAmount] = useState('');
  const [note, setNote] = useState('');
  const [transactionType, setTransactionType] = useState<'Expense' | 'Income'>('Expense');
  const [paymentMethod, setPaymentMethod] = useState<'Cash' | 'Transfer'>('Cash');
  const [categories, setCategories] = useState<Category[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState<number | undefined>(undefined);
  const [loading, setLoading] = useState(false);
  const [loadingCats, setLoadingCats] = useState(true);

  useEffect(() => {
    fetchCategories();
  }, [transactionType]);

  const fetchCategories = async () => {
    try {
      const res = await categoryService.getCategories();
      if (res.success && res.data) {
        const filteredCats = res.data.filter((c) => c.type === transactionType);
        setCategories(filteredCats);
        if (filteredCats.length > 0) {
          setSelectedCategoryId(filteredCats[0].categoryId);
        } else {
          setSelectedCategoryId(undefined);
        }
      }
    } catch (e: any) {
      console.error('Lỗi khi tải danh mục:', e);
    } finally {
      setLoadingCats(false);
    }
  };

  const handleSave = async () => {
    if (!title.trim()) {
      Alert.alert('Lỗi', 'Vui lòng nhập tiêu đề chi tiêu.');
      return;
    }

    const numAmount = parseFloat(amount.replace(/[^0-9]/g, ''));
    if (isNaN(numAmount) || numAmount <= 0) {
      Alert.alert('Lỗi', 'Vui lòng nhập số tiền hợp lệ lớn hơn 0.');
      return;
    }

    setLoading(true);
    try {
      const payload = {
        title: title.trim(),
        amount: numAmount,
        categoryId: selectedCategoryId,
        spentAt: new Date().toISOString(),
        paymentMethod,
        note: note.trim(),
      };

      const res = await expenseService.createExpense(payload);
      if (res.success) {
        Alert.alert('Thành công', 'Đã lưu khoản chi tiêu!', [
          { text: 'OK', onPress: () => navigation.goBack() },
        ]);
      }
    } catch (e: any) {
      Alert.alert('Lỗi', e.response?.data?.message || e.message || 'Không thể tạo khoản chi tiêu.');
    } finally {
      setLoading(false);
    }
  };

  const getCategoryIcon = (iconName?: string): keyof typeof MaterialCommunityIcons.glyphMap => {
    switch (iconName?.toLowerCase()) {
      case 'food': return 'silverware-fork-knife';
      case 'transport': return 'motorbike';
      case 'book': return 'book-open-page-variant-outline';
      case 'game': return 'gamepad-variant-outline';
      case 'home': return 'home-outline';
      case 'health': return 'medical-bag';
      case 'shopping': return 'shopping-outline';
      case 'other': return 'dots-horizontal-circle-outline';
      default: return 'dots-horizontal-circle-outline';
    }
  };

  return (
    <PageContainer>
      <ScrollView style={styles.container} keyboardShouldPersistTaps="handled">
        <AppCard style={styles.card}>
          <Text style={styles.sectionLabel}>Loại giao dịch</Text>
          <View style={styles.segmentRow}>
            {(['Expense', 'Income'] as const).map((type) => (
              <TouchableOpacity
                key={type}
                style={[styles.segmentButton, transactionType === type && styles.segmentButtonActive]}
                onPress={() => setTransactionType(type)}
              >
                <Text style={[styles.segmentText, transactionType === type && styles.segmentTextActive]}>
                  {type === 'Expense' ? 'Chi tiêu' : 'Thu nhập'}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          <Text style={styles.sectionLabel}>Tiền từ đâu</Text>
          <View style={styles.segmentRow}>
            {(['Cash', 'Transfer'] as const).map((method) => (
              <TouchableOpacity
                key={method}
                style={[styles.segmentButton, paymentMethod === method && styles.segmentButtonActive]}
                onPress={() => setPaymentMethod(method)}
              >
                <Text style={[styles.segmentText, paymentMethod === method && styles.segmentTextActive]}>
                  {method === 'Cash' ? 'Tiền mặt' : 'Chuyển khoản'}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          <AppInput
            label="Tiêu đề khoản chi"
            placeholder="Nhập tên khoản chi"
            value={title}
            onChangeText={setTitle}
          />

          <AppInput
            label="Số tiền (VND)"
            placeholder="Nhập số tiền"
            keyboardType="numeric"
            value={amount}
            onChangeText={(text) => {
              // Only allow numbers
              const cleaned = text.replace(/[^0-9]/g, '');
              setAmount(cleaned);
            }}
          />

          <AppInput
            label="Ghi chú thêm (Không bắt buộc)"
            placeholder="Thêm ghi chú nếu cần"
            value={note}
            onChangeText={setNote}
            multiline
            numberOfLines={2}
            style={{ height: 60, textAlignVertical: 'top' }}
          />

          {/* Category selection grid */}
          <Text style={styles.sectionLabel}>{transactionType === 'Expense' ? 'Chọn danh mục chi tiêu' : 'Chọn danh mục thu nhập'}</Text>
          {loadingCats ? (
            <Text style={styles.loadingText}>Đang tải danh mục...</Text>
          ) : (
            <View style={styles.grid}>
              {categories.map((cat) => {
                const isSelected = selectedCategoryId === cat.categoryId;
                const catColor = cat.color || '#6366F1';
                return (
                  <TouchableOpacity
                    key={cat.categoryId}
                    style={[
                      styles.gridItem,
                      isSelected && { borderColor: catColor, backgroundColor: catColor + '10' },
                    ]}
                    onPress={() => setSelectedCategoryId(cat.categoryId)}
                    activeOpacity={0.7}
                  >
                    <MaterialCommunityIcons
                      name={getCategoryIcon(cat.icon)}
                      size={22}
                      color={isSelected ? catColor : '#64748B'}
                      style={styles.catIcon}
                    />
                    <Text style={[styles.catName, isSelected && { color: catColor, fontWeight: '700' }]}>
                      {cat.name}
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </View>
          )}

          <AppButton
            title="LƯU GHI CHÉP"
            loading={loading}
            onPress={handleSave}
            style={styles.saveButton}
          />
        </AppCard>
      </ScrollView>
    </PageContainer>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: 'transparent',
    padding: 16,
  },
  card: {
    padding: 20,
    marginBottom: 32,
  },
  sectionLabel: {
    fontSize: 14,
    fontWeight: '700',
    color: COLORS.text,
    marginTop: 8,
    marginBottom: 12,
    fontFamily: 'System',
  },
  loadingText: {
    fontSize: 13,
    color: COLORS.textMuted,
    fontFamily: 'System',
    marginBottom: 16,
  },
  segmentRow: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 12,
  },
  segmentButton: {
    flex: 1,
    minHeight: 42,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    backgroundColor: COLORS.cardBgLight,
    alignItems: 'center',
    justifyContent: 'center',
  },
  segmentButtonActive: {
    borderColor: COLORS.primary,
    backgroundColor: 'rgba(184, 77, 53, 0.12)',
  },
  segmentText: {
    fontSize: 13,
    fontWeight: '700',
    color: COLORS.textMuted,
    fontFamily: 'System',
  },
  segmentTextActive: {
    color: COLORS.primary,
  },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    marginHorizontal: -4,
    marginBottom: 20,
  },
  gridItem: {
    width: '30%',
    marginHorizontal: '1.66%',
    marginVertical: 6,
    padding: 10,
    borderRadius: 8,
    borderWidth: 2,
    borderColor: COLORS.cardBorder,
    backgroundColor: COLORS.cardBgLight,
    alignItems: 'center',
    justifyContent: 'center',
  },
  catIcon: {
    marginBottom: 4,
  },
  catName: {
    fontSize: 11,
    color: COLORS.text,
    fontWeight: '500',
    fontFamily: 'System',
    textAlign: 'center',
  },
  saveButton: {
    marginTop: 12,
  },
});

export default AddExpenseScreen;

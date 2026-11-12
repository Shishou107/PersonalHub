import React, { useEffect, useMemo, useState } from 'react';
import { Alert, FlatList, Linking, Platform, RefreshControl, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { Feather, Ionicons } from '@expo/vector-icons';
import bankTransactionService from '../../services/bankTransactionService';
import { BankTransaction } from '../../types/bankTransaction';
import AppCard from '../../components/AppCard';
import Loading from '../../components/Loading';
import { formatMoney } from '../../utils/formatMoney';
import { formatDate } from '../../utils/formatDate';
import PageContainer from '../../components/PageContainer';
import { COLORS } from '../../utils/theme';
import {
  isBackgroundPermissionAvailable,
  isIgnoringBatteryOptimizations,
  openAutoStartSettings,
  openBatteryOptimizationSettings,
  rebindNotificationListener,
} from '../../utils/backgroundPermission';
import { getApiErrorMessage } from '../../utils/apiError';

const ALL_DAYS = 'Tất cả';
const pad2 = (value: number) => String(value).padStart(2, '0');

const BankTransactionsScreen = ({ navigation }: any) => {
  const now = new Date();
  const [txs, setTxs] = useState<BankTransaction[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [year, setYear] = useState(now.getFullYear());
  const [selectedDay, setSelectedDay] = useState(ALL_DAYS);
  const [batteryAllowed, setBatteryAllowed] = useState<boolean | null>(null);

  const fetchTransactions = async (showLoading = true) => {
    if (showLoading) setLoading(true);
    try {
      // Automatically keep the notification listener service alive & re-bound
      rebindNotificationListener().catch(() => {});
      const firstDay = `${year}-${pad2(month)}-01`;
      const lastDay = `${year}-${pad2(month)}-${pad2(new Date(year, month, 0).getDate())}`;
      const res = await bankTransactionService.getTransactions(firstDay, lastDay);
      if (res.success && res.data) {
        setTxs(res.data);
      }
    } catch (e: any) {
      Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể tải giao dịch thông báo.'));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    const unsubscribe = navigation.addListener('focus', () => {
      rebindNotificationListener().catch(() => {});
      fetchTransactions(false);
    });
    fetchTransactions(true);
    return unsubscribe;
  }, [navigation, month, year]);

  useEffect(() => {
    setSelectedDay(ALL_DAYS);
  }, [month, year]);

  useEffect(() => {
    if (!isBackgroundPermissionAvailable) return;
    isIgnoringBatteryOptimizations()
      .then(setBatteryAllowed)
      .catch(() => setBatteryAllowed(null));
  }, []);

  const dayOptions = useMemo(
    () => [ALL_DAYS, ...Array.from({ length: new Date(year, month, 0).getDate() }, (_, i) => pad2(i + 1))],
    [month, year]
  );

  const filteredTxs = useMemo(() => txs.filter((item) => {
    if (selectedDay === ALL_DAYS) return true;
    const txDate = new Date(item.transactionTime || item.createdAt);
    return pad2(txDate.getDate()) === selectedDay;
  }), [txs, selectedDay]);

  const pendingCount = txs.filter((item) => !item.isConfirmed).length;
  const confirmedCount = txs.length - pendingCount;
  const incomeTotal = txs
    .filter((item) => item.transactionType === 'Income')
    .reduce((sum, item) => sum + item.amount, 0);
  const expenseTotal = txs
    .filter((item) => item.transactionType === 'Expense')
    .reduce((sum, item) => sum + item.amount, 0);

  const moveMonth = (direction: -1 | 1) => {
    if (direction < 0 && month === 1) {
      setMonth(12);
      setYear((value) => value - 1);
      return;
    }

    if (direction > 0 && month === 12) {
      setMonth(1);
      setYear((value) => value + 1);
      return;
    }

    setMonth((value) => value + direction);
  };

  const openNotificationAccessSettings = async () => {
    try {
      if (Platform.OS === 'android' && (Linking as any).sendIntent) {
        await (Linking as any).sendIntent('android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS');
        return;
      }
      await Linking.openSettings();
    } catch {
      Alert.alert('Cài đặt thông báo', 'Vui lòng vào Cài đặt Android và bật quyền truy cập thông báo cho ứng dụng.');
    }
  };

  const openBackgroundRunSettings = async () => {
    if (!isBackgroundPermissionAvailable) {
      Alert.alert('Chạy nền', 'Thiết bị này không hỗ trợ mở nhanh cài đặt chạy nền từ app.');
      return;
    }

    try {
      await openBatteryOptimizationSettings();
      Alert.alert(
        'Giữ app chạy nền',
        'Nếu Android hỏi, hãy chọn Cho phép/Allow để app không bị tắt khi màn hình khóa. Sau đó quay lại app và bấm tiếp phần Tự khởi động nếu máy có mục đó.'
      );
      const allowed = await isIgnoringBatteryOptimizations().catch(() => null);
      setBatteryAllowed(allowed);
    } catch {
      Alert.alert('Chạy nền', 'Không mở được cài đặt pin. Bạn hãy vào Cài đặt > Ứng dụng > AI Daily Expense > Pin và chọn Không giới hạn.');
    }
  };

  const openAutoStartRunSettings = async () => {
    if (!isBackgroundPermissionAvailable) {
      Alert.alert('Tự khởi động', 'Thiết bị này không hỗ trợ mở nhanh cài đặt tự khởi động từ app.');
      return;
    }

    try {
      await openAutoStartSettings();
      Alert.alert(
        'Tự khởi động',
        'Nếu thấy mục Tự khởi động/Khởi chạy ứng dụng/Launch app, hãy bật cho AI Daily Expense và tắt quản lý tự động nếu máy yêu cầu.'
      );
    } catch {
      Alert.alert('Tự khởi động', 'Không mở được trang tự khởi động. Bạn hãy tìm mục Khởi chạy ứng dụng hoặc Tự khởi động trong cài đặt pin/bảo mật của máy.');
    }
  };

  const getTransactionMeta = (item: BankTransaction) => {
    if (item.transactionType === 'Income') {
      return {
        sign: '+',
        color: '#059669',
        softColor: '#ECFDF5',
        icon: 'arrow-down-left' as keyof typeof Feather.glyphMap,
        label: 'Tiền vào',
      };
    }

    if (item.transactionType === 'Expense') {
      return {
        sign: '-',
        color: '#DC2626',
        softColor: '#FEF2F2',
        icon: 'arrow-up-right' as keyof typeof Feather.glyphMap,
        label: 'Tiền ra',
      };
    }

    return {
      sign: '',
      color: COLORS.text,
      softColor: COLORS.cardBgLight,
      icon: 'repeat' as keyof typeof Feather.glyphMap,
      label: 'Khác',
    };
  };

  const handleConfirm = (item: BankTransaction) => {
    const isIncome = item.transactionType === 'Income';
    const message = isIncome
      ? 'Ghi giao dịch này vào danh sách thu nhập?'
      : 'Ghi giao dịch này vào danh sách chi tiêu?';

    Alert.alert('Xác nhận giao dịch', message, [
      { text: 'Hủy', style: 'cancel' },
      {
        text: 'Xác nhận',
        onPress: async () => {
          try {
            const res = await bankTransactionService.confirmTransaction(item.bankTransactionId, {});
            if (res.success) {
              Alert.alert('Thành công', isIncome ? 'Đã ghi nhận thu nhập.' : 'Đã ghi nhận chi tiêu.');
              fetchTransactions(false);
            }
          } catch (e: any) {
            Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể xác nhận giao dịch.'));
          }
        },
      },
    ]);
  };

  const handleDelete = (item: BankTransaction) => {
    Alert.alert('Xóa giao dịch', 'Bạn có chắc muốn xóa giao dịch này?', [
      { text: 'Hủy', style: 'cancel' },
      {
        text: 'Xóa',
        style: 'destructive',
        onPress: async () => {
          try {
            const res = await bankTransactionService.deleteTransaction(item.bankTransactionId);
            if (res.success) fetchTransactions(false);
          } catch (e: any) {
            Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể xóa giao dịch.'));
          }
        },
      },
    ]);
  };

  const renderTransaction = ({ item }: { item: BankTransaction }) => {
    const meta = getTransactionMeta(item);
    const title = item.description || item.bankName || 'Giao dịch';

    return (
      <AppCard style={[styles.txCard, !item.isConfirmed && styles.pendingCard]}>
        <View style={styles.txHeader}>
          <View style={[styles.txIconBox, { backgroundColor: meta.softColor }]}>
            <Feather name={meta.icon} size={18} color={meta.color} />
          </View>

          <View style={styles.txMain}>
            <View style={styles.titleRow}>
              <Text style={styles.txTitle} numberOfLines={1}>{title}</Text>
              <View style={[styles.statusPill, item.isConfirmed ? styles.donePill : styles.pendingPill]}>
                <Text style={[styles.statusText, item.isConfirmed ? styles.doneText : styles.pendingText]}>
                  {item.isConfirmed ? 'Đã lưu' : 'Chờ lưu'}
                </Text>
              </View>
            </View>
            <Text style={styles.txSub} numberOfLines={1}>
              {item.bankName || 'Ngân hàng'} · {formatDate(item.transactionTime || item.createdAt)}
            </Text>
          </View>
        </View>

        <View style={styles.amountRow}>
          <View style={styles.metaPill}>
            <Ionicons name="pricetag-outline" size={13} color="#64748B" />
            <Text style={styles.metaText} numberOfLines={1}>{item.categoryName || 'Chưa phân loại'}</Text>
          </View>
          <Text style={[styles.txAmount, { color: meta.color }]} numberOfLines={1} adjustsFontSizeToFit>
            {meta.sign}{formatMoney(item.amount)}
          </Text>
        </View>

        {item.rawMessage ? (
          <Text style={styles.rawMessage} numberOfLines={2}>{item.rawMessage}</Text>
        ) : null}

        {!item.isConfirmed && (item.transactionType === 'Expense' || item.transactionType === 'Income') ? (
          <View style={styles.actionRow}>
            <TouchableOpacity style={styles.confirmBtn} onPress={() => handleConfirm(item)}>
              <Ionicons name="checkmark" size={16} color="#FFFFFF" />
              <Text style={styles.confirmBtnText}>Xác nhận</Text>
            </TouchableOpacity>
            <TouchableOpacity style={styles.deleteBtn} onPress={() => handleDelete(item)}>
              <Ionicons name="trash-outline" size={17} color="#DC2626" />
            </TouchableOpacity>
          </View>
        ) : null}
      </AppCard>
    );
  };

  if (loading) {
    return <Loading fullScreen message="Đang tải giao dịch..." />;
  }

  return (
    <PageContainer style={{ padding: 16 }}>
      <View style={styles.monthRow}>
        <TouchableOpacity style={styles.navBtn} onPress={() => moveMonth(-1)}>
          <Feather name="chevron-left" size={20} color={COLORS.text} />
        </TouchableOpacity>
        <View style={styles.monthTitleWrap}>
          <Text style={styles.monthTitle}>Tháng {month}</Text>
          <Text style={styles.monthSub}>{year}</Text>
        </View>
        <TouchableOpacity style={styles.navBtn} onPress={() => moveMonth(1)}>
          <Feather name="chevron-right" size={20} color={COLORS.text} />
        </TouchableOpacity>
      </View>

      <View style={styles.summaryRow}>
        <View style={[styles.summaryBox, styles.incomeBox]}>
          <Text style={styles.summaryLabel}>Tiền vào</Text>
          <Text style={[styles.summaryValue, styles.incomeText]} numberOfLines={1} adjustsFontSizeToFit>
            +{formatMoney(incomeTotal)}
          </Text>
        </View>
        <View style={[styles.summaryBox, styles.expenseBox]}>
          <Text style={styles.summaryLabel}>Tiền ra</Text>
          <Text style={[styles.summaryValue, styles.expenseText]} numberOfLines={1} adjustsFontSizeToFit>
            -{formatMoney(expenseTotal)}
          </Text>
        </View>
      </View>

      <View style={styles.statusRow}>
        <View style={styles.statusSummary}>
          <Ionicons name="time-outline" size={15} color="#D97706" />
          <Text style={styles.statusSummaryText}>{pendingCount} chờ lưu</Text>
        </View>
        <View style={styles.statusSummary}>
          <Ionicons name="checkmark-circle-outline" size={15} color="#059669" />
          <Text style={styles.statusSummaryText}>{confirmedCount} đã lưu</Text>
        </View>
      </View>

      <TouchableOpacity style={styles.permissionCard} onPress={openNotificationAccessSettings} activeOpacity={0.84}>
        <View style={styles.permissionIcon}>
          <Ionicons name="notifications-outline" size={18} color="#0F766E" />
        </View>
        <View style={styles.permissionTextWrap}>
          <Text style={styles.permissionTitle}>Quyền đọc thông báo</Text>
          <Text style={styles.permissionSub} numberOfLines={1}>Mở cài đặt nếu sau một lúc app không còn nhận giao dịch.</Text>
        </View>
        <Feather name="chevron-right" size={18} color="#64748B" />
      </TouchableOpacity>

      <View style={styles.backgroundCard}>
        <View style={styles.backgroundHeader}>
          <View style={[styles.permissionIcon, styles.backgroundIcon]}>
            <Ionicons name="shield-checkmark-outline" size={18} color={COLORS.primaryDark} />
          </View>
          <View style={styles.permissionTextWrap}>
            <Text style={styles.permissionTitle}>Giữ app chạy nền</Text>
            <Text style={styles.permissionSub} numberOfLines={1}>
              {batteryAllowed === true ? 'Đã bỏ giới hạn pin. Kiểm tra thêm tự khởi động nếu vẫn mất thông báo.' : 'Bật để app tiếp tục đọc giao dịch khi khóa màn hình.'}
            </Text>
          </View>
        </View>

        <View style={styles.backgroundActions}>
          <TouchableOpacity style={styles.backgroundBtn} onPress={openBackgroundRunSettings} activeOpacity={0.84}>
            <Ionicons name="battery-charging-outline" size={16} color={COLORS.primaryDark} />
            <Text style={styles.backgroundBtnText}>Không giới hạn pin</Text>
          </TouchableOpacity>
          <TouchableOpacity style={styles.backgroundBtn} onPress={openAutoStartRunSettings} activeOpacity={0.84}>
            <Ionicons name="rocket-outline" size={16} color={COLORS.primaryDark} />
            <Text style={styles.backgroundBtnText}>Tự khởi động</Text>
          </TouchableOpacity>
        </View>
      </View>

      <View style={styles.daySelectorWrapper}>
        <FlatList
          horizontal
          data={dayOptions}
          keyExtractor={(item) => item}
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={styles.daySelectorContent}
          renderItem={({ item }) => {
            const selected = selectedDay === item;
            return (
              <TouchableOpacity
                style={[styles.dayButton, selected && styles.dayButtonSelected]}
                onPress={() => setSelectedDay(item)}
                activeOpacity={0.8}
              >
                <Text style={[styles.dayButtonText, selected && styles.dayButtonTextSelected]}>
                  {item === ALL_DAYS ? item : `Ngày ${item}`}
                </Text>
              </TouchableOpacity>
            );
          }}
        />
      </View>

      <View style={styles.sectionHeader}>
        <Text style={styles.sectionTitle}>
          {selectedDay === ALL_DAYS ? 'Giao dịch từ thông báo' : `Giao dịch ngày ${selectedDay}/${month}`}
        </Text>
        <Text style={styles.sectionCount}>{filteredTxs.length}</Text>
      </View>

      <FlatList
        data={filteredTxs}
        keyExtractor={(item) => String(item.bankTransactionId)}
        renderItem={renderTransaction}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => { setRefreshing(true); fetchTransactions(false); }} colors={[COLORS.primary]} tintColor={COLORS.primary} />}
        ListEmptyComponent={
          <View style={styles.emptyContainer}>
            <Ionicons name="file-tray-outline" size={30} color={COLORS.textMuted} />
            <Text style={styles.emptyText}>Chưa có giao dịch</Text>
          </View>
        }
        contentContainerStyle={styles.listContainer}
        showsVerticalScrollIndicator={false}
      />
    </PageContainer>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: 'transparent',
    padding: 16,
  },
  monthRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 10,
  },
  navBtn: {
    width: 38,
    height: 38,
    borderRadius: 8,
    backgroundColor: COLORS.cardBgLight,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    alignItems: 'center',
    justifyContent: 'center',
  },
  monthTitleWrap: {
    alignItems: 'center',
  },
  monthTitle: {
    fontSize: 18,
    fontWeight: '900',
    color: COLORS.text,
    fontFamily: 'System',
  },
  monthSub: {
    fontSize: 12,
    color: COLORS.textMuted,
    fontWeight: '700',
    fontFamily: 'System',
    marginTop: 1,
  },
  summaryRow: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 8,
  },
  summaryBox: {
    flex: 1,
    borderRadius: 8,
    padding: 12,
    borderWidth: 1,
  },
  incomeBox: {
    backgroundColor: 'rgba(236, 253, 245, 0.8)',
    borderColor: 'rgba(167, 243, 208, 0.6)',
  },
  expenseBox: {
    backgroundColor: 'rgba(254, 242, 242, 0.8)',
    borderColor: 'rgba(254, 202, 202, 0.6)',
  },
  summaryLabel: {
    fontSize: 12,
    fontWeight: '700',
    color: COLORS.textMuted,
    fontFamily: 'System',
    marginBottom: 4,
  },
  summaryValue: {
    fontSize: 17,
    fontWeight: '900',
    fontFamily: 'System',
  },
  incomeText: {
    color: '#047857',
  },
  expenseText: {
    color: '#B91C1C',
  },
  statusRow: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 10,
  },
  statusSummary: {
    flex: 1,
    minHeight: 34,
    borderRadius: 8,
    backgroundColor: COLORS.cardBgLight,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 5,
  },
  statusSummaryText: {
    fontSize: 12,
    fontWeight: '800',
    color: COLORS.text,
    fontFamily: 'System',
  },
  permissionCard: {
    minHeight: 54,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    backgroundColor: COLORS.cardBgLight,
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 10,
    marginBottom: 10,
  },
  permissionIcon: {
    width: 36,
    height: 36,
    borderRadius: 8,
    backgroundColor: 'rgba(76, 101, 143, 0.15)',
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 10,
  },
  permissionTextWrap: {
    flex: 1,
    minWidth: 0,
  },
  permissionTitle: {
    fontSize: 13,
    fontWeight: '900',
    color: COLORS.text,
    fontFamily: 'System',
  },
  permissionSub: {
    fontSize: 11,
    color: COLORS.textMuted,
    fontWeight: '600',
    fontFamily: 'System',
    marginTop: 2,
  },
  backgroundCard: {
    borderRadius: 8,
    borderWidth: 1,
    borderColor: 'rgba(184, 77, 53, 0.2)',
    backgroundColor: 'rgba(255, 255, 255, 0.72)',
    padding: 10,
    marginBottom: 10,
  },
  backgroundHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 9,
  },
  backgroundIcon: {
    backgroundColor: 'rgba(236, 170, 164, 0.28)',
  },
  backgroundActions: {
    flexDirection: 'row',
    gap: 8,
  },
  backgroundBtn: {
    flex: 1,
    minHeight: 38,
    borderRadius: 8,
    backgroundColor: 'rgba(255, 255, 255, 0.76)',
    borderWidth: 1,
    borderColor: 'rgba(184, 77, 53, 0.16)',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 8,
    gap: 5,
  },
  backgroundBtnText: {
    flexShrink: 1,
    fontSize: 11,
    fontWeight: '900',
    color: COLORS.primaryDark,
    fontFamily: 'System',
    textAlign: 'center',
  },
  daySelectorWrapper: {
    borderBottomWidth: 1,
    borderBottomColor: COLORS.cardBorder,
    paddingBottom: 9,
    marginBottom: 12,
  },
  daySelectorContent: {
    gap: 6,
  },
  dayButton: {
    paddingHorizontal: 12,
    paddingVertical: 7,
    borderRadius: 8,
    backgroundColor: COLORS.cardBgLight,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
  },
  dayButtonSelected: {
    backgroundColor: COLORS.primary,
    borderColor: COLORS.primary,
  },
  dayButtonText: {
    fontSize: 12,
    fontWeight: '800',
    color: COLORS.text,
    fontFamily: 'System',
  },
  dayButtonTextSelected: {
    color: '#FFFFFF',
  },
  sectionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 10,
  },
  sectionTitle: {
    fontSize: 15,
    fontWeight: '900',
    color: COLORS.text,
    fontFamily: 'System',
  },
  sectionCount: {
    minWidth: 30,
    textAlign: 'center',
    borderRadius: 8,
    overflow: 'hidden',
    backgroundColor: COLORS.cardBgLight,
    color: COLORS.primary,
    paddingVertical: 4,
    paddingHorizontal: 8,
    fontSize: 12,
    fontWeight: '900',
    fontFamily: 'System',
  },
  listContainer: {
    paddingBottom: 28,
  },
  txCard: {
    padding: 13,
    marginBottom: 10,
    backgroundColor: COLORS.cardBg,
    borderColor: COLORS.cardBorder,
  },
  pendingCard: {
    borderColor: COLORS.accent,
  },
  txHeader: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  txIconBox: {
    width: 40,
    height: 40,
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 10,
  },
  txMain: {
    flex: 1,
    minWidth: 0,
  },
  titleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  txTitle: {
    flex: 1,
    minWidth: 0,
    fontSize: 14,
    fontWeight: '900',
    color: COLORS.text,
    fontFamily: 'System',
  },
  txSub: {
    fontSize: 12,
    color: COLORS.textMuted,
    fontWeight: '600',
    fontFamily: 'System',
    marginTop: 3,
  },
  statusPill: {
    borderRadius: 6,
    paddingHorizontal: 7,
    paddingVertical: 4,
  },
  donePill: {
    backgroundColor: 'rgba(236, 253, 245, 0.8)',
  },
  pendingPill: {
    backgroundColor: 'rgba(255, 251, 235, 0.8)',
  },
  statusText: {
    fontSize: 11,
    fontWeight: '900',
    fontFamily: 'System',
  },
  doneText: {
    color: '#059669',
  },
  pendingText: {
    color: '#D97706',
  },
  amountRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: 12,
    gap: 10,
  },
  metaPill: {
    flex: 1,
    minWidth: 0,
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: COLORS.cardBgLight,
    borderRadius: 6,
    paddingHorizontal: 8,
    paddingVertical: 6,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
  },
  metaText: {
    flex: 1,
    minWidth: 0,
    fontSize: 12,
    color: COLORS.textMuted,
    fontWeight: '800',
    fontFamily: 'System',
    marginLeft: 5,
  },
  txAmount: {
    maxWidth: 156,
    fontSize: 17,
    fontWeight: '900',
    fontFamily: 'System',
    textAlign: 'right',
  },
  rawMessage: {
    fontSize: 12,
    color: COLORS.textMuted,
    lineHeight: 17,
    fontFamily: 'System',
    marginTop: 10,
  },
  actionRow: {
    flexDirection: 'row',
    marginTop: 12,
    borderTopWidth: 1,
    borderTopColor: COLORS.cardBorder,
    paddingTop: 12,
    gap: 8,
  },
  confirmBtn: {
    flex: 1,
    height: 40,
    backgroundColor: COLORS.primary,
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
    flexDirection: 'row',
  },
  confirmBtnText: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '900',
    fontFamily: 'System',
    marginLeft: 5,
  },
  deleteBtn: {
    width: 44,
    height: 40,
    backgroundColor: 'rgba(254, 242, 242, 0.8)',
    borderWidth: 1,
    borderColor: 'rgba(254, 202, 202, 0.6)',
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
  },
  emptyContainer: {
    paddingVertical: 42,
    alignItems: 'center',
  },
  emptyText: {
    fontSize: 14,
    fontWeight: '900',
    color: COLORS.textMuted,
    fontFamily: 'System',
    marginTop: 10,
  },
});

export default BankTransactionsScreen;

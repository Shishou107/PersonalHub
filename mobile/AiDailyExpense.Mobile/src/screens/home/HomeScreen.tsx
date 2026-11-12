import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, RefreshControl, TouchableOpacity, Alert, Platform, Modal, TextInput } from 'react-native';
import { useAuth } from '../../context/AuthContext';
import reportService from '../../services/reportService';
import taskService from '../../services/taskService';
import { syncAllPendingTasksNotifications } from '../../utils/notificationHelper';
import { DashboardData } from '../../types/report';
import SummaryCard from '../../components/SummaryCard';
import ExpenseItem from '../../components/ExpenseItem';
import TaskItem from '../../components/TaskItem';
import Loading from '../../components/Loading';
import AppButton from '../../components/AppButton';
import { formatMoney, formatMoneyCompact } from '../../utils/formatMoney';
import PageContainer from '../../components/PageContainer';
import { COLORS } from '../../utils/theme';
import { getApiErrorMessage } from '../../utils/apiError';

const HomeScreen = ({ navigation }: any) => {
  const { user, logout, initializeBalance } = useAuth();
  const [data, setData] = useState<DashboardData | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [balanceModalVisible, setBalanceModalVisible] = useState(false);
  const [cashBalance, setCashBalance] = useState('');
  const [bankBalance, setBankBalance] = useState('');
  const [savingBalance, setSavingBalance] = useState(false);

  const fetchDashboard = async (showLoading = true) => {
    if (showLoading) setLoading(true);
    try {
      const res = await reportService.getDashboard();
      if (res.success && res.data) {
        setData(res.data);
      }
    } catch (e: any) {
      if (e.response?.status === 401) {
        await logout();
        Alert.alert('Phiên đăng nhập hết hạn', 'Vui lòng đăng nhập lại để tải dữ liệu.');
        return;
      }
      console.error('Lỗi khi tải thông tin tổng quan:', e);
      Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể tải dữ liệu tổng quan.'));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    const unsubscribe = navigation.addListener('focus', () => {
      fetchDashboard(false);
      syncAllPendingTasksNotifications().catch((err) => console.error('Error syncing notifications:', err));
    });
    fetchDashboard(true);
    syncAllPendingTasksNotifications().catch((err) => console.error('Error syncing notifications:', err));
    return unsubscribe;
  }, [navigation]);

  useEffect(() => {
    if (!loading && user && !user.hasInitializedBalance) {
      setBalanceModalVisible(true);
    }
  }, [loading, user]);

  const onRefresh = () => {
    setRefreshing(true);
    fetchDashboard(false);
  };

  const handleCompleteTask = async (taskId: number) => {
    try {
      const res = await taskService.completeTask(taskId);
      if (res.success) {
        fetchDashboard(false);
        syncAllPendingTasksNotifications().catch((err) => console.error('Error syncing notifications:', err));
      }
    } catch (e: any) {
      Alert.alert('Lỗi', e.message || 'Không thể hoàn thành công việc.');
    }
  };

  const handleLogout = () => {
    Alert.alert('Đăng xuất', 'Bạn có chắc chắn muốn đăng xuất?', [
      { text: 'Hủy', style: 'cancel' },
      { text: 'Đăng xuất', onPress: logout, style: 'destructive' },
    ]);
  };

  const parseAmount = (value: string) => {
    const num = parseFloat(value.replace(/[^0-9]/g, ''));
    return isNaN(num) ? 0 : num;
  };

  const handleInitializeBalance = async () => {
    setSavingBalance(true);
    try {
      await initializeBalance(parseAmount(cashBalance), parseAmount(bankBalance));
      setBalanceModalVisible(false);
      setCashBalance('');
      setBankBalance('');
      fetchDashboard(false);
    } catch (e: any) {
      Alert.alert('Lỗi', e.response?.data?.message || e.message || 'Không thể lưu số dư ban đầu.');
    } finally {
      setSavingBalance(false);
    }
  };

  if (loading) {
    return <Loading fullScreen message="Đang tải dữ liệu tổng quan..." />;
  }

  return (
    <PageContainer>
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.contentContainer}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} colors={[COLORS.primary]} />}
    >
      <View style={styles.header}>
        <View style={styles.headerText}>
          <Text style={styles.welcomeText}>Xin chào</Text>
          <Text style={styles.userName} numberOfLines={1}>{user?.fullName || 'Người dùng'}</Text>
        </View>
        <TouchableOpacity style={styles.logoutButton} onPress={handleLogout}>
          <Text style={styles.logoutText}>Thoát</Text>
        </TouchableOpacity>
      </View>

      <View style={styles.balanceBox}>
        <Text style={styles.balanceLabel}>Ví tiền của tôi</Text>
        <Text style={styles.balanceTotal} numberOfLines={1} adjustsFontSizeToFit>
          {formatMoney(data?.totalBalance ?? user?.totalBalance ?? 0)}
        </Text>
        <View style={styles.balanceRow}>
          <View style={[styles.balanceMini, styles.cashMini]}>
            <Text style={[styles.balanceMiniLabel, styles.cashLabel]}>Tiền mặt</Text>
            <Text
              style={[styles.balanceMiniValue, styles.cashValue]}
              numberOfLines={1}
              adjustsFontSizeToFit
              minimumFontScale={0.72}
            >
              {formatMoneyCompact(data?.cashBalance ?? user?.cashBalance ?? 0)}
            </Text>
          </View>
          <View style={[styles.balanceMini, styles.bankMini]}>
            <Text style={[styles.balanceMiniLabel, styles.bankLabel]}>Chuyển khoản</Text>
            <Text
              style={[styles.balanceMiniValue, styles.bankValue]}
              numberOfLines={1}
              adjustsFontSizeToFit
              minimumFontScale={0.72}
            >
              {formatMoneyCompact(data?.bankBalance ?? user?.bankBalance ?? 0)}
            </Text>
          </View>
        </View>
      </View>

      <View style={styles.row}>
        <SummaryCard
          title="Hôm nay"
          value={formatMoneyCompact(data?.todayExpense || 0)}
          color="#DC2626"
        />
        <SummaryCard
          title="Tháng này"
          value={formatMoneyCompact(data?.monthExpense || 0)}
          color="#2563EB"
        />
      </View>

      <View style={styles.row}>
        <SummaryCard
          title="Công việc quá hạn"
          value={String(data?.overdueTasks || 0)}
          color="#B91C1C"
        />
        <SummaryCard
          title="Tổng tiền"
          value={formatMoneyCompact(data?.totalBalance || 0)}
          color="#0F766E"
        />
      </View>

      <View style={styles.row}>
        <SummaryCard
          title="Công việc chờ"
          value={String(data?.pendingTasks || 0)}
          color="#D97706"
        />
        <SummaryCard
          title="Công việc hoàn thành"
          value={String(data?.completedTasks || 0)}
          color="#059669"
        />
      </View>

      <Text style={styles.sectionTitle}>Thao tác nhanh</Text>
      <View style={styles.actionsContainer}>
        <TouchableOpacity
          style={styles.actionButton}
          onPress={() => navigation.navigate('ExpensesTab', { screen: 'AddExpense' })}
        >
          <Text style={styles.actionText}>Thêm chi tiêu</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.actionButton}
          onPress={() => navigation.navigate('TasksTab', { screen: 'AddTask' })}
        >
          <Text style={styles.actionText}>Thêm việc</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.actionButton}
          onPress={() => navigation.navigate('StudyTab', { screen: 'StudyReview' })}
        >
          <Text style={styles.actionText}>Ôn bài</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.actionButton}
          onPress={() => navigation.navigate('AiTab')}
        >
          <Text style={styles.actionText}>Trợ lý AI</Text>
        </TouchableOpacity>
      </View>

      <View style={styles.sectionHeaderRow}>
        <Text style={styles.sectionTitle}>Công việc hôm nay</Text>
        <TouchableOpacity onPress={() => navigation.navigate('TasksTab')}>
          <Text style={styles.viewAllText}>Xem tất cả</Text>
        </TouchableOpacity>
      </View>
      <View style={styles.listContainer}>
        {data?.todayTasks && data.todayTasks.length > 0 ? (
          data.todayTasks.slice(0, 3).map((task) => (
            <TaskItem
              key={task.taskId}
              item={task}
              onComplete={handleCompleteTask}
              onPress={() => navigation.navigate('TasksTab', { screen: 'TaskList' })}
            />
          ))
        ) : (
          <View style={styles.emptyContainer}>
            <Text style={styles.emptyText}>Hôm nay bạn không có công việc nào.</Text>
          </View>
        )}
      </View>

      <View style={styles.sectionHeaderRow}>
        <Text style={styles.sectionTitle}>Chi tiêu gần đây</Text>
        <TouchableOpacity onPress={() => navigation.navigate('ExpensesTab')}>
          <Text style={styles.viewAllText}>Xem tất cả</Text>
        </TouchableOpacity>
      </View>
      <View style={[styles.listContainer, styles.expensesList]}>
        {data?.recentExpenses && data.recentExpenses.length > 0 ? (
          data.recentExpenses.map((expense) => (
            <ExpenseItem key={expense.expenseId} item={expense} />
          ))
        ) : (
          <View style={styles.emptyContainer}>
            <Text style={styles.emptyText}>Bạn chưa ghi chép chi tiêu gần đây.</Text>
          </View>
        )}
      </View>
    </ScrollView>

    <Modal visible={balanceModalVisible} transparent animationType="fade" onRequestClose={() => {}}>
      <View style={styles.modalOverlay}>
        <View style={styles.modalCard}>
          <Text style={styles.modalTitle}>Thiết lập số dư ban đầu</Text>
          <Text style={styles.modalText}>Nhập số tiền hiện tại một lần để app tự cộng trừ sau mỗi giao dịch.</Text>
          <TextInput
            style={styles.modalInput}
            placeholder="Tiền mặt"
            keyboardType="numeric"
            value={cashBalance}
            onChangeText={(text) => setCashBalance(text.replace(/[^0-9]/g, ''))}
          />
          <TextInput
            style={styles.modalInput}
            placeholder="Chuyển khoản"
            keyboardType="numeric"
            value={bankBalance}
            onChangeText={(text) => setBankBalance(text.replace(/[^0-9]/g, ''))}
          />
          <AppButton title="Lưu số dư" loading={savingBalance} onPress={handleInitializeBalance} />
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
  contentContainer: {
    padding: 16,
    paddingBottom: 34,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 18,
    marginTop: Platform.OS === 'ios' ? 10 : 8,
  },
  headerText: {
    flex: 1,
    minWidth: 0,
    marginRight: 12,
  },
  welcomeText: {
    fontSize: 14,
    color: COLORS.secondary,
    fontFamily: 'System',
  },
  userName: {
    fontSize: 22,
    fontWeight: '800',
    color: COLORS.primaryDark,
    fontFamily: 'System',
    marginTop: 2,
  },
  logoutButton: {
    height: 38,
    paddingHorizontal: 14,
    borderRadius: 8,
    backgroundColor: 'rgba(76, 101, 143, 0.12)',
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: COLORS.secondary,
  },
  logoutText: {
    fontSize: 13,
    fontWeight: '700',
    color: COLORS.secondary,
    fontFamily: 'System',
  },
  balanceBox: {
    backgroundColor: COLORS.cardBg,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    padding: 14,
    marginBottom: 10,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 6,
    elevation: 2,
  },
  balanceLabel: {
    fontSize: 13,
    fontWeight: '700',
    color: COLORS.primary,
    fontFamily: 'System',
  },
  balanceTotal: {
    fontSize: 26,
    fontWeight: '800',
    color: COLORS.primaryDark,
    fontFamily: 'System',
    marginTop: 4,
  },
  balanceRow: {
    flexDirection: 'row',
    gap: 8,
    marginTop: 12,
  },
  balanceMini: {
    flex: 1,
    backgroundColor: 'rgba(255, 255, 255, 0.72)',
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    borderRadius: 8,
    padding: 10,
  },
  cashMini: {
    backgroundColor: 'rgba(236, 253, 245, 0.85)',
    borderColor: 'rgba(167, 243, 208, 0.5)',
  },
  bankMini: {
    backgroundColor: 'rgba(239, 246, 255, 0.85)',
    borderColor: 'rgba(191, 219, 254, 0.5)',
  },
  balanceMiniLabel: {
    fontSize: 12,
    color: COLORS.textMuted,
    fontFamily: 'System',
    marginBottom: 4,
  },
  cashLabel: {
    color: '#047857',
  },
  bankLabel: {
    color: '#1D4ED8',
  },
  balanceMiniValue: {
    fontSize: 14,
    fontWeight: '800',
    color: COLORS.text,
    fontFamily: 'System',
  },
  cashValue: {
    color: '#065F46',
  },
  bankValue: {
    color: '#1E40AF',
  },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginHorizontal: -4,
    marginBottom: 8,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: COLORS.primaryDark,
    marginVertical: 12,
    fontFamily: 'System',
  },
  sectionHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginTop: 12,
  },
  viewAllText: {
    fontSize: 13,
    fontWeight: '700',
    color: COLORS.primary,
    fontFamily: 'System',
  },
  actionsContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    marginBottom: 12,
    gap: 8,
  },
  actionButton: {
    width: '48.7%',
    minHeight: 44,
    backgroundColor: COLORS.cardBgLight,
    borderRadius: 8,
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    paddingHorizontal: 8,
  },
  actionText: {
    fontSize: 12,
    fontWeight: '700',
    color: COLORS.text,
    fontFamily: 'System',
    textAlign: 'center',
  },
  listContainer: {
    backgroundColor: COLORS.cardBg,
    borderRadius: 12,
    paddingHorizontal: 12,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    paddingVertical: 6,
  },
  expensesList: {
    paddingVertical: 2,
  },
  emptyContainer: {
    paddingVertical: 24,
    justifyContent: 'center',
    alignItems: 'center',
  },
  emptyText: {
    fontSize: 13,
    color: COLORS.textMuted,
    fontFamily: 'System',
    textAlign: 'center',
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(45, 35, 39, 0.5)', // using dark charcoal shade with opacity
    alignItems: 'center',
    justifyContent: 'center',
    padding: 20,
  },
  modalCard: {
    width: '100%',
    maxWidth: 420,
    backgroundColor: COLORS.cardBg,
    borderRadius: 12,
    padding: 18,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: '800',
    color: COLORS.text,
    fontFamily: 'System',
  },
  modalText: {
    fontSize: 13,
    color: COLORS.textMuted,
    lineHeight: 18,
    marginTop: 6,
    marginBottom: 14,
    fontFamily: 'System',
  },
  modalInput: {
    height: 48,
    borderWidth: 1,
    borderColor: COLORS.border,
    borderRadius: 8,
    paddingHorizontal: 12,
    fontSize: 15,
    color: COLORS.text,
    backgroundColor: 'rgba(255, 255, 255, 0.75)',
    marginBottom: 10,
    fontFamily: 'System',
  },
});

export default HomeScreen;

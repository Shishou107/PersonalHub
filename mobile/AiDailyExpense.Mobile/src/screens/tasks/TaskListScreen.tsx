import React, { useEffect, useMemo, useState } from 'react';
import { View, Text, StyleSheet, ScrollView, RefreshControl, TouchableOpacity, Alert } from 'react-native';
import taskService from '../../services/taskService';
import { Task } from '../../types/task';
import TaskItem from '../../components/TaskItem';
import Loading from '../../components/Loading';
import { syncAllPendingTasksNotifications } from '../../utils/notificationHelper';
import PageContainer from '../../components/PageContainer';
import { COLORS } from '../../utils/theme';
import { getApiErrorMessage } from '../../utils/apiError';
import ListToDo from '../../components/ListToDo';
type CalendarView = 'month' | 'week' | 'year';
const WEEK_DAYS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
const MONTH_NAMES = ['Thg 1', 'Thg 2', 'Thg 3', 'Thg 4', 'Thg 5', 'Thg 6', 'Thg 7', 'Thg 8', 'Thg 9', 'Thg 10', 'Thg 11', 'Thg 12'];

const TaskListScreen = ({ navigation }: any) => {
  const [tasks, setTasks] = useState<Task[]>([]);
  const [statusFilter, setStatusFilter] = useState<'Pending' | 'Done'>('Pending');
  const [calendarView, setCalendarView] = useState<CalendarView>('month');
  const [currentDate, setCurrentDate] = useState(new Date());
  const [selectedDate, setSelectedDate] = useState(new Date());
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const parseTaskDate = (task: Task) => {
    const value = task.taskType === 'Schedule'
      ? (task.startAt || task.dueAt)
      : task.dueAt;

    if (!value) return null;
    const date = new Date(value);
    return isNaN(date.getTime()) ? null : date;
  };

  const startOfDay = (date: Date) => new Date(date.getFullYear(), date.getMonth(), date.getDate());

  const isSameDay = (a: Date, b: Date) => (
    a.getFullYear() === b.getFullYear()
    && a.getMonth() === b.getMonth()
    && a.getDate() === b.getDate()
  );

  const isToday = (date: Date) => isSameDay(date, new Date());

  const startOfWeek = (date: Date) => {
    const day = date.getDay();
    const diff = day === 0 ? -6 : 1 - day;
    const start = startOfDay(date);
    start.setDate(start.getDate() + diff);
    return start;
  };

  const formatShortDate = (date: Date) => `${String(date.getDate()).padStart(2, '0')}/${String(date.getMonth() + 1).padStart(2, '0')}`;

  const tasksWithDate = useMemo(() => tasks.filter((task) => parseTaskDate(task)), [tasks]);
  const undatedTasks = useMemo(() => tasks.filter((task) => !parseTaskDate(task)), [tasks]);

  const getTasksForDate = (date: Date) => tasksWithDate.filter((task) => {
    const taskDate = parseTaskDate(task);
    return taskDate ? isSameDay(taskDate, date) : false;
  });

  const getTasksForMonth = (year: number, month: number) => tasksWithDate.filter((task) => {
    const taskDate = parseTaskDate(task);
    return taskDate ? taskDate.getFullYear() === year && taskDate.getMonth() === month : false;
  });

  const selectedTasks = useMemo(() => {
    if (calendarView === 'year') {
      return getTasksForMonth(currentDate.getFullYear(), currentDate.getMonth());
    }
    return getTasksForDate(selectedDate);
  }, [calendarView, currentDate, selectedDate, tasksWithDate]);

  const selectedTitle = calendarView === 'year'
    ? `${MONTH_NAMES[currentDate.getMonth()]} ${currentDate.getFullYear()}`
    : formatShortDate(selectedDate);

  const fetchTasks = async (showLoading = true) => {
    if (showLoading) setLoading(true);
    try {
      const res = await taskService.getTasks(undefined, statusFilter);
      if (res.success && res.data) {
        setTasks(res.data);
      }
    } catch (e: any) {
      console.error('Lỗi khi tải danh sách công việc:', e);
      Alert.alert('Lỗi', getApiErrorMessage(e, 'Không thể tải danh sách công việc.'));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    const unsubscribe = navigation.addListener('focus', () => {
      fetchTasks(false);
      syncAllPendingTasksNotifications().catch((err) => console.error('Error syncing notifications:', err));
    });
    fetchTasks(true);
    syncAllPendingTasksNotifications().catch((err) => console.error('Error syncing notifications:', err));
    return unsubscribe;
  }, [navigation, statusFilter]);

  const onRefresh = () => {
    setRefreshing(true);
    fetchTasks(false);
  };

  const handleCompleteTask = async (id: number) => {
    try {
      const res = await taskService.completeTask(id);
      if (res.success) {
        fetchTasks(false);
        syncAllPendingTasksNotifications().catch((err) => console.error('Error syncing notifications:', err));
      }
    } catch (e: any) {
      Alert.alert('Lỗi', e.message || 'Không thể hoàn thành công việc.');
    }
  };

  const handleDeleteTask = (id: number, title: string) => {
    Alert.alert('Xóa công việc', `Bạn có chắc chắn muốn xóa "${title}"?`, [
      { text: 'Hủy', style: 'cancel' },
      {
        text: 'Xóa',
        style: 'destructive',
        onPress: async () => {
          try {
            const res = await taskService.deleteTask(id);
            if (res.success) {
              fetchTasks(false);
              syncAllPendingTasksNotifications().catch((err) => console.error('Error syncing notifications:', err));
            }
          } catch (e: any) {
            Alert.alert('Lỗi', e.message || 'Không thể xóa công việc.');
          }
        },
      },
    ]);
  };

  const movePeriod = (direction: -1 | 1) => {
    const next = new Date(currentDate);
    if (calendarView === 'month') next.setMonth(next.getMonth() + direction);
    if (calendarView === 'week') next.setDate(next.getDate() + direction * 7);
    if (calendarView === 'year') next.setFullYear(next.getFullYear() + direction);
    setCurrentDate(next);
    setSelectedDate(next);
  };

  const periodTitle = () => {
    if (calendarView === 'year') return String(currentDate.getFullYear());
    if (calendarView === 'month') return `Tháng ${currentDate.getMonth() + 1}, ${currentDate.getFullYear()}`;

    const start = startOfWeek(currentDate);
    const end = new Date(start);
    end.setDate(start.getDate() + 6);
    return `${formatShortDate(start)} - ${formatShortDate(end)}/${end.getFullYear()}`;
  };

  const selectDate = (date: Date) => {
    setSelectedDate(date);
    setCurrentDate(date);
  };

  const renderTaskCount = (count: number) => {
    if (count === 0) return null;
    return (
      <View style={styles.countPill}>
        <Text style={styles.countText}>{count}</Text>
      </View>
    );
  };

  const renderMonthCalendar = () => {
    const monthStart = new Date(currentDate.getFullYear(), currentDate.getMonth(), 1);
    const gridStart = startOfWeek(monthStart);
    const days = Array.from({ length: 42 }, (_, index) => {
      const date = new Date(gridStart);
      date.setDate(gridStart.getDate() + index);
      return date;
    });

    return (
      <View style={styles.calendarCard}>
        <View style={styles.weekHeader}>
          {WEEK_DAYS.map((day) => (
            <Text key={day} style={styles.weekHeaderText}>{day}</Text>
          ))}
        </View>
        <View style={styles.monthGrid}>
          {days.map((date) => {
            const inMonth = date.getMonth() === currentDate.getMonth();
            const count = getTasksForDate(date).length;
            const selected = isSameDay(date, selectedDate);

            return (
              <TouchableOpacity
                key={`${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`}
                style={[
                  styles.dayCell,
                  !inMonth && styles.mutedCell,
                  isToday(date) && styles.todayCell,
                  selected && styles.selectedCell,
                ]}
                onPress={() => selectDate(date)}
              >
                <Text
                  style={[
                    styles.dayNumber,
                    !inMonth && styles.mutedText,
                    selected && styles.selectedText,
                  ]}
                >
                  {date.getDate()}
                </Text>
                {renderTaskCount(count)}
              </TouchableOpacity>
            );
          })}
        </View>
      </View>
    );
  };

  const renderWeekCalendar = () => {
    const weekStart = startOfWeek(currentDate);
    const days = Array.from({ length: 7 }, (_, index) => {
      const date = new Date(weekStart);
      date.setDate(weekStart.getDate() + index);
      return date;
    });

    return (
      <View style={styles.calendarCard}>
        <View style={styles.weekGrid}>
          {days.map((date, index) => {
            const count = getTasksForDate(date).length;
            const selected = isSameDay(date, selectedDate);

            return (
              <TouchableOpacity
                key={`${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`}
                style={[
                  styles.weekDayCell,
                  isToday(date) && styles.todayCell,
                  selected && styles.selectedCell,
                ]}
                onPress={() => selectDate(date)}
              >
                <Text style={[styles.weekDayLabel, selected && styles.selectedText]}>{WEEK_DAYS[index]}</Text>
                <Text style={[styles.weekDayNumber, selected && styles.selectedText]}>{date.getDate()}</Text>
                {renderTaskCount(count)}
              </TouchableOpacity>
            );
          })}
        </View>
      </View>
    );
  };

  const renderYearCalendar = () => (
    <View style={styles.calendarCard}>
      <View style={styles.yearGrid}>
        {MONTH_NAMES.map((monthName, index) => {
          const count = getTasksForMonth(currentDate.getFullYear(), index).length;
          const selected = index === currentDate.getMonth();

          return (
            <TouchableOpacity
              key={monthName}
              style={[styles.monthCell, selected && styles.selectedCell]}
              onPress={() => {
                const next = new Date(currentDate.getFullYear(), index, 1);
                setCurrentDate(next);
                setSelectedDate(next);
              }}
            >
              <Text style={[styles.monthName, selected && styles.selectedText]}>{monthName}</Text>
              <Text style={[styles.monthCount, selected && styles.selectedText]}>{count} việc</Text>
            </TouchableOpacity>
          );
        })}
      </View>
    </View>
  );

  const renderCalendar = () => {
    if (calendarView === 'week') return renderWeekCalendar();
    if (calendarView === 'year') return renderYearCalendar();
    return renderMonthCalendar();
  };

  if (loading) {
    return <Loading fullScreen message="Đang tải lịch công việc..." />;
  }

  return (
    <PageContainer>
      <ScrollView
        contentContainerStyle={styles.contentContainer}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} colors={[COLORS.primary]} tintColor={COLORS.primary} />}
      >
        <View style={styles.segmentGroup}>
          <TouchableOpacity
            style={[styles.segmentButton, statusFilter === 'Pending' && styles.segmentActive]}
            onPress={() => setStatusFilter('Pending')}
          >
            <Text style={[styles.segmentText, statusFilter === 'Pending' && styles.segmentTextActive]}>Đang chờ</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.segmentButton, statusFilter === 'Done' && styles.segmentActive]}
            onPress={() => setStatusFilter('Done')}
          >
            <Text style={[styles.segmentText, statusFilter === 'Done' && styles.segmentTextActive]}>Đã xong</Text>
          </TouchableOpacity>
        </View>

        <View style={styles.segmentGroup}>
          {(['month', 'week', 'year'] as CalendarView[]).map((view) => (
            <TouchableOpacity
              key={view}
              style={[styles.segmentButton, calendarView === view && styles.segmentActive]}
              onPress={() => setCalendarView(view)}
            >
              <Text style={[styles.segmentText, calendarView === view && styles.segmentTextActive]}>
                {view === 'month' ? 'Tháng' : view === 'week' ? 'Tuần' : 'Năm'}
              </Text>
            </TouchableOpacity>
          ))}
        </View>

        <View style={styles.periodRow}>
          <TouchableOpacity style={styles.periodButton} onPress={() => movePeriod(-1)}>
            <Text style={styles.periodButtonText}>‹</Text>
          </TouchableOpacity>
          <Text style={styles.periodTitle} numberOfLines={1}>{periodTitle()}</Text>
          <TouchableOpacity style={styles.periodButton} onPress={() => movePeriod(1)}>
            <Text style={styles.periodButtonText}>›</Text>
          </TouchableOpacity>
        </View>

        {renderCalendar()}

        <View style={styles.selectedHeader}>
          <View>
            <Text style={styles.sectionTitle}>
              {calendarView === 'year' ? 'Công việc trong tháng' : 'Công việc trong ngày'}
            </Text>
            <Text style={styles.sectionSub}>{selectedTitle}</Text>
          </View>
          <Text style={styles.taskTotal}>{selectedTasks.length}</Text>
        </View>

        <View style={styles.taskList}>
          {selectedTasks.length > 0 ? (
            selectedTasks.map((task) => (
              <TaskItem
                key={task.taskId}
                item={task}
                onComplete={handleCompleteTask}
                onPress={() => handleDeleteTask(task.taskId, task.title)}
              />
            ))
          ) : (
            <View style={styles.emptyContainer}>
              <Text style={styles.emptyText}>Không có công việc trong mốc này.</Text>
            </View>
          )}
        </View>

        {false && undatedTasks.length > 0 ? (
          <>
            <View style={styles.selectedHeader}>
              <View>
                <Text style={styles.sectionTitle}>Chưa đặt ngày</Text>
                <Text style={styles.sectionSub}>Các việc chưa có hạn cụ thể</Text>
              </View>
              <Text style={styles.taskTotal}>{undatedTasks.length}</Text>
            </View>
            <View style={styles.taskList}>
              {undatedTasks.map((task) => (
                <TaskItem
                  key={task.taskId}
                  item={task}
                  onComplete={handleCompleteTask}
                  onPress={() => handleDeleteTask(task.taskId, task.title)}
                />
              ))}
            </View>
          </>
        ) : null}
        <ListToDo tasks={tasks} onCompleteTask={handleCompleteTask} />
      </ScrollView>

      <TouchableOpacity
        style={styles.fab}
        onPress={() => navigation.navigate('AddTask')}
        activeOpacity={0.85}
      >
        <Text style={styles.fabText}>+</Text>
      </TouchableOpacity>
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
    paddingBottom: 96,
  },
  segmentGroup: {
    flexDirection: 'row',
    backgroundColor: COLORS.cardBg,
    borderRadius: 8,
    padding: 3,
    marginBottom: 10,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
  },
  segmentButton: {
    flex: 1,
    minHeight: 36,
    borderRadius: 6,
    alignItems: 'center',
    justifyContent: 'center',
  },
  segmentActive: {
    backgroundColor: COLORS.primary,
  },
  segmentText: {
    fontSize: 13,
    fontWeight: '700',
    color: COLORS.text,
    fontFamily: 'System',
  },
  segmentTextActive: {
    color: '#FFFFFF',
  },
  periodRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginVertical: 8,
  },
  periodButton: {
    width: 38,
    height: 38,
    borderRadius: 8,
    backgroundColor: COLORS.cardBgLight,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
  },
  periodButtonText: {
    fontSize: 26,
    lineHeight: 28,
    fontWeight: '500',
    color: COLORS.text,
    fontFamily: 'System',
  },
  periodTitle: {
    flex: 1,
    marginHorizontal: 12,
    textAlign: 'center',
    fontSize: 18,
    fontWeight: '800',
    color: COLORS.text,
    fontFamily: 'System',
  },
  calendarCard: {
    backgroundColor: COLORS.cardBg,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    padding: 10,
  },
  weekHeader: {
    flexDirection: 'row',
    marginBottom: 6,
  },
  weekHeaderText: {
    width: '14.2857%',
    textAlign: 'center',
    fontSize: 11,
    fontWeight: '700',
    color: COLORS.textMuted,
    fontFamily: 'System',
  },
  monthGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
  },
  dayCell: {
    width: '14.2857%',
    height: 54,
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
  },
  mutedCell: {
    opacity: 0.42,
  },
  todayCell: {
    borderWidth: 1,
    borderColor: COLORS.accent,
  },
  selectedCell: {
    backgroundColor: COLORS.primary,
  },
  dayNumber: {
    fontSize: 14,
    fontWeight: '700',
    color: COLORS.text,
    fontFamily: 'System',
  },
  mutedText: {
    color: COLORS.textMuted,
  },
  selectedText: {
    color: '#FFFFFF',
  },
  countPill: {
    minWidth: 18,
    height: 18,
    borderRadius: 9,
    backgroundColor: COLORS.cardBgLight,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 5,
    marginTop: 4,
  },
  countText: {
    fontSize: 10,
    fontWeight: '800',
    color: COLORS.primary,
    fontFamily: 'System',
  },
  weekGrid: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  weekDayCell: {
    width: '13.2%',
    minHeight: 76,
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: COLORS.cardBgLight,
  },
  weekDayLabel: {
    fontSize: 11,
    fontWeight: '700',
    color: COLORS.textMuted,
    fontFamily: 'System',
    marginBottom: 4,
  },
  weekDayNumber: {
    fontSize: 18,
    fontWeight: '800',
    color: COLORS.text,
    fontFamily: 'System',
  },
  yearGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'space-between',
  },
  monthCell: {
    width: '31.5%',
    minHeight: 70,
    borderRadius: 8,
    backgroundColor: COLORS.cardBgLight,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 8,
    padding: 6,
  },
  monthName: {
    fontSize: 14,
    fontWeight: '800',
    color: COLORS.text,
    fontFamily: 'System',
    marginBottom: 5,
  },
  monthCount: {
    fontSize: 12,
    fontWeight: '600',
    color: COLORS.textMuted,
    fontFamily: 'System',
  },
  selectedHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: 18,
    marginBottom: 8,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '800',
    color: COLORS.text,
    fontFamily: 'System',
  },
  sectionSub: {
    fontSize: 12,
    color: COLORS.textMuted,
    marginTop: 2,
    fontFamily: 'System',
  },
  taskTotal: {
    minWidth: 34,
    textAlign: 'center',
    paddingVertical: 6,
    paddingHorizontal: 10,
    borderRadius: 8,
    overflow: 'hidden',
    backgroundColor: COLORS.cardBgLight,
    color: COLORS.primary,
    fontSize: 14,
    fontWeight: '800',
    fontFamily: 'System',
  },
  taskList: {
    backgroundColor: COLORS.cardBg,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    padding: 10,
  },
  emptyContainer: {
    paddingVertical: 24,
    alignItems: 'center',
    justifyContent: 'center',
  },
  emptyText: {
    fontSize: 13,
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
});

export default TaskListScreen;

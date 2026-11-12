import React, { useMemo } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { COLORS } from '../utils/theme';
import { Task } from '../types/task';

interface ListToDoProps {
  tasks: Task[];
  onCompleteTask?: (id: number) => void;
}

const WEEK_DAYS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

const ListToDo: React.FC<ListToDoProps> = ({ tasks, onCompleteTask }) => {
  // Lọc chỉ lấy các công việc (bỏ lịch trình)
  const onlyTasks = useMemo(() => {
    return tasks.filter((task) => task.taskType !== 'Schedule');
  }, [tasks]);

  const parseTaskDate = (task: Task) => {
    const value = task.dueAt || task.startAt;
    if (!value) return null;
    const date = new Date(value);
    return isNaN(date.getTime()) ? null : date;
  };

  const currentWeekDays = useMemo(() => {
    const now = new Date();
    const day = now.getDay();
    const diffToMonday = day === 0 ? -6 : 1 - day; // Tìm ngày Thứ 2

    const monday = new Date(now);
    monday.setDate(now.getDate() + diffToMonday);
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date(monday);
      d.setDate(monday.getDate() + i);
      return d;
    });
  }, []);

  // Gom nhóm các task theo tên công việc (title) và vị trí Thứ (0 -> 6)
  const groupedTasksMap = useMemo(() => {
    const map: { [title: string]: { [dayIndex: number]: Task } } = {};
    onlyTasks.forEach((task) => {
      const taskDate = parseTaskDate(task);
      if (!taskDate) return;
      currentWeekDays.forEach((weekDate, index) => {
        if (
          taskDate.getFullYear() === weekDate.getFullYear() &&
          taskDate.getMonth() === weekDate.getMonth() &&
          taskDate.getDate() === weekDate.getDate()
        ) {
          if (!map[task.title]) {
            map[task.title] = {};
          }
          map[task.title][index] = task;
        }
      });
    });
    return map;
  }, [onlyTasks, currentWeekDays]);

  return (
    <View style={styles.container}>
      <View style={styles.headerTitleRow}>
        <Ionicons name="calendar-outline" size={18} color={COLORS.primary} style={{ marginRight: 6 }} />
        <Text style={styles.title}>Bảng công việc theo tuần</Text>
      </View>

      <ScrollView horizontal showsHorizontalScrollIndicator={false}>
        <View style={styles.table}>
          <View style={styles.headerRow}>
            <Text style={[styles.headerCell, styles.taskColumn]}>
              Công việc
            </Text>
            {WEEK_DAYS.map((day, idx) => (
              <Text key={day} style={[styles.headerCell, idx >= 5 && styles.weekendHeaderCell]}>
                {day}
              </Text>
            ))}
          </View>
          {Object.keys(groupedTasksMap).length > 0 ? (
            Object.entries(groupedTasksMap).map(([title, dayMap]) => (
              <View key={title} style={styles.bodyRow}>
                {/* Cột 1: Tên công việc */}
                <Text style={[styles.taskNameCell, styles.taskColumn]} numberOfLines={1}>
                  {title}
                </Text>
                {/* 7 Cột cho 7 Thứ trong tuần */}
                {currentWeekDays.map((_, dayIndex) => {
                  const taskInDay = dayMap[dayIndex];
                  // Nếu ngày này không có task
                  if (!taskInDay) {
                    return (
                      <View key={dayIndex} style={styles.cell}>
                        <Text style={styles.emptyText}>-</Text>
                      </View>
                    );
                  }
                  const isCompleted = taskInDay.status === 'Done';

                  // Nếu có task ➔ Hiển thị nút tích có thể bấm
                  return (
                    <TouchableOpacity
                      key={dayIndex}
                      style={styles.cell}
                      activeOpacity={0.7}
                      onPress={() => onCompleteTask && onCompleteTask(taskInDay.taskId)}
                    >
                      <Ionicons
                        name={isCompleted ? 'checkmark-circle' : 'ellipse-outline'}
                        size={18}
                        color={isCompleted ? COLORS.primary : COLORS.textMuted}
                      />
                    </TouchableOpacity>
                  );
                })}
              </View>
            ))
          ) : (
            <View style={styles.emptyContainer}>
              <Text style={styles.emptyText}>Không có công việc nào trong tuần này</Text>
            </View>
          )}
        </View>
      </ScrollView>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    marginTop: 16,
    padding: 14,
    backgroundColor: COLORS.cardBg,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
  },
  headerTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 12,
  },
  title: {
    fontSize: 15,
    fontWeight: '700',
    color: COLORS.text,
    fontFamily: 'System',
  },
  table: {
    minWidth: 360,
  },
  headerRow: {
    flexDirection: 'row',
    borderBottomWidth: 1,
    borderBottomColor: COLORS.cardBorder,
    paddingBottom: 8,
    marginBottom: 6,
  },
  headerCell: {
    width: 38,
    textAlign: 'center',
    fontWeight: '700',
    fontSize: 12,
    color: COLORS.textMuted,
    fontFamily: 'System',
  },
  weekendHeaderCell: {
    color: COLORS.primary,
  },
  taskColumn: {
    width: 120,
    textAlign: 'left',
  },
  bodyRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
    borderBottomWidth: 1,
    borderBottomColor: COLORS.cardBorder,
  },
  taskNameCell: {
    fontSize: 13,
    fontWeight: '600',
    color: COLORS.text,
    fontFamily: 'System',
  },
  cell: {
    width: 38,
    alignItems: 'center',
    justifyContent: 'center',
  },
  emptyContainer: {
    paddingVertical: 14,
    alignItems: 'center',
  },
  emptyText: {
    color: COLORS.textMuted,
    fontSize: 12,
    opacity: 0.6,
  },
});

export default ListToDo;
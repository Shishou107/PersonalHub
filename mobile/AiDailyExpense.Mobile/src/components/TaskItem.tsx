import React from 'react';
import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Task } from '../types/task';
import { formatDate, formatDateOnly } from '../utils/formatDate';
import { COLORS } from '../utils/theme';

interface TaskItemProps {
  item: Task;
  onComplete?: (id: number) => void;
  onPress?: () => void;
}

const TaskItem: React.FC<TaskItemProps> = ({ item, onComplete, onPress }) => {
  const isCompleted = item.status === 'Done';
  const isOverdue = !!item.isOverdue;
  const isSchedule = item.taskType === 'Schedule';

  const getPriorityColor = (priority: string) => {
    switch (priority) {
      case 'High': return '#DC2626';
      case 'Normal': return COLORS.secondary;
      case 'Low': return '#059669';
      default: return COLORS.textMuted;
    }
  };

  const getPriorityText = (priority: string) => {
    switch (priority) {
      case 'High': return 'Cao';
      case 'Normal': return 'Vừa';
      case 'Low': return 'Thấp';
      default: return 'Thường';
    }
  };

  const formatTime = (value?: string) => {
    if (!value) return '';
    const date = new Date(value);
    if (isNaN(date.getTime())) return '';
    return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;
  };

  const getScheduleText = () => {
    if (!isSchedule) {
      return item.dueAt ? `H\u1ea1n: ${formatDate(item.dueAt)}` : 'Ch\u01b0a \u0111\u1eb7t h\u1ea1n';
    }

    if (item.startAt && item.endAt) {
      const startDate = new Date(item.startAt);
      const endDate = new Date(item.endAt);
      const isCrossDay = !isNaN(startDate.getTime())
        && !isNaN(endDate.getTime())
        && (
          startDate.getFullYear() !== endDate.getFullYear()
          || startDate.getMonth() !== endDate.getMonth()
          || startDate.getDate() !== endDate.getDate()
        );

      if (isCrossDay) {
        return `${formatDateOnly(item.startAt)} ${formatTime(item.startAt)} - ${formatDateOnly(item.endAt)} ${formatTime(item.endAt)}`;
      }

      return `${formatDateOnly(item.startAt)}  ${formatTime(item.startAt)} - ${formatTime(item.endAt)}`;
    }

    if (item.dueAt) {
      return `Hạn: ${formatDate(item.dueAt)}`;
    }

    return 'Chưa đặt hạn';
  };

  const priorityColor = getPriorityColor(item.priority);
  const badgeText = isSchedule ? 'Lịch trình' : (isOverdue ? 'Quá hạn' : getPriorityText(item.priority));
  const badgeColor = isSchedule ? COLORS.primary : (isOverdue ? '#B91C1C' : priorityColor);

  return (
    <TouchableOpacity
      style={[styles.container, isCompleted && styles.completedContainer]}
      onPress={onPress}
      activeOpacity={0.72}
    >
      {isSchedule ? (
        <View style={styles.scheduleIconContainer}>
          <Ionicons name="calendar-outline" size={20} color={COLORS.primary} />
        </View>
      ) : (
        <TouchableOpacity
          style={[styles.checkbox, isCompleted && styles.checkboxChecked]}
          onPress={() => onComplete && onComplete(item.taskId)}
          disabled={isCompleted}
        >
          {isCompleted ? <Text style={styles.checkmark}>✓</Text> : null}
        </TouchableOpacity>
      )}

      <View style={styles.textContainer}>
        <Text style={[styles.title, isCompleted && styles.completedTitle]} numberOfLines={1}>
          {item.title}
        </Text>
        <Text style={[styles.date, isCompleted && styles.completedDate]} numberOfLines={1}>
          {isOverdue ? `Quá hạn - ${getScheduleText()}` : getScheduleText()}
        </Text>
        
        {item.location ? (
          <View style={styles.infoRow}>
            <Ionicons name="location-outline" size={12} color="#64748B" style={styles.infoIcon} />
            <Text style={styles.infoText} numberOfLines={1}>{item.location}</Text>
          </View>
        ) : null}

        {item.description ? (
          <View style={styles.infoRow}>
            <Ionicons name="document-text-outline" size={12} color="#64748B" style={styles.infoIcon} />
            <Text style={styles.infoText} numberOfLines={2}>{item.description}</Text>
          </View>
        ) : null}
      </View>

      <View style={[styles.priorityBadge, { backgroundColor: `${badgeColor}14` }]}>
        <Text style={[styles.priorityText, { color: badgeColor }]}>
          {badgeText}
        </Text>
      </View>
    </TouchableOpacity>
  );
};

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 12,
    backgroundColor: COLORS.cardBg,
    borderRadius: 8,
    marginBottom: 8,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
  },
  completedContainer: {
    backgroundColor: COLORS.cardBgLight,
    borderColor: COLORS.cardBorder,
  },
  checkbox: {
    width: 22,
    height: 22,
    borderRadius: 6,
    borderWidth: 2,
    borderColor: COLORS.secondary,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 10,
  },
  checkboxChecked: {
    backgroundColor: '#059669',
    borderColor: '#059669',
  },
  checkboxDisabled: {
    backgroundColor: '#FEE2E2',
    borderColor: '#FCA5A5',
  },
  checkmark: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: 'bold',
    fontFamily: 'System',
    lineHeight: 16,
  },
  scheduleIconContainer: {
    width: 22,
    height: 22,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 10,
  },
  textContainer: {
    flex: 1,
    minWidth: 0,
    marginRight: 8,
  },
  title: {
    fontSize: 15,
    fontWeight: '700',
    color: COLORS.text,
    fontFamily: 'System',
    marginBottom: 4,
  },
  completedTitle: {
    textDecorationLine: 'line-through',
    color: COLORS.textMuted,
  },
  date: {
    fontSize: 12,
    color: COLORS.textMuted,
    fontFamily: 'System',
  },
  completedDate: {
    color: COLORS.cardBorder,
  },
  infoRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 4,
  },
  infoIcon: {
    marginRight: 4,
  },
  infoText: {
    fontSize: 11,
    color: COLORS.textMuted,
    fontFamily: 'System',
    flex: 1,
  },
  priorityBadge: {
    minWidth: 46,
    alignItems: 'center',
    paddingHorizontal: 8,
    paddingVertical: 5,
    borderRadius: 6,
  },
  priorityText: {
    fontSize: 11,
    fontWeight: '800',
    fontFamily: 'System',
  },
});

export default TaskItem;

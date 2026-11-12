import React, { useState, useMemo } from 'react';
import { View, Text, StyleSheet, ScrollView, Alert, TouchableOpacity, Platform } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import taskService from '../../services/taskService';
import AppInput from '../../components/AppInput';
import AppButton from '../../components/AppButton';
import AppCard from '../../components/AppCard';
import PageContainer from '../../components/PageContainer';
import { COLORS } from '../../utils/theme';
import { CreateTaskPayload } from '../../types/task';

const WEEK_DAYS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

const pad2 = (value: number) => String(value).padStart(2, '0');

const toDateKey = (date: Date) => `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`;

const parseDateKey = (key: string) => {
  const [y, m, d] = key.split('-').map(Number);
  return new Date(y, m - 1, d);
};

const formatDateDisplay = (date: Date) => `${pad2(date.getDate())}/${pad2(date.getMonth() + 1)}/${date.getFullYear()}`;
const formatShortDate = (date: Date) => `${pad2(date.getDate())}/${pad2(date.getMonth() + 1)}`;

const toLocalIsoString = (date: Date) => (
  `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`
  + `T${pad2(date.getHours())}:${pad2(date.getMinutes())}:${pad2(date.getSeconds())}`
);

const startOfWeek = (date: Date) => {
  const d = new Date(date);
  const day = d.getDay();
  const diff = (day === 0 ? -6 : 1) - day;
  d.setDate(d.getDate() + diff);
  d.setHours(0, 0, 0, 0);
  return d;
};

const isSameDay = (d1: Date, d2: Date) => (
  d1.getFullYear() === d2.getFullYear()
  && d1.getMonth() === d2.getMonth()
  && d1.getDate() === d2.getDate()
);

const AddTaskScreen = ({ navigation }: any) => {
  const today = useMemo(() => new Date(), []);
  const todayKey = useMemo(() => toDateKey(today), [today]);

  const [taskType, setTaskType] = useState<'Task' | 'Schedule'>('Task');
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [location, setLocation] = useState('');

  // Calendar State
  const [calendarMonth, setCalendarMonth] = useState<Date>(new Date(today.getFullYear(), today.getMonth(), 1));
  const [selectedDates, setSelectedDates] = useState<string[]>([todayKey]);

  // Time & Options
  const [startTime, setStartTime] = useState('08:00');
  const [endTime, setEndTime] = useState('09:00');
  const [priority, setPriority] = useState<'Low' | 'Normal' | 'High'>('Normal');
  const [repeatType, setRepeatType] = useState<'None' | 'Daily' | 'Weekly' | 'Monthly' | 'Yearly'>('None');
  const [reminderBeforeMinutes, setReminderBeforeMinutes] = useState<number | undefined>(undefined);
  const [customReminder, setCustomReminder] = useState('');
  const [isCustomActive, setIsCustomActive] = useState(false);
  const [loading, setLoading] = useState(false);

  const formatTimeText = (value: string) => {
    const digits = value.replace(/\D/g, '').slice(0, 4);
    if (digits.length <= 2) return digits;
    return `${digits.slice(0, 2)}:${digits.slice(2)}`;
  };

  const parseTime = (value: string) => {
    const match = value.trim().match(/^([01]?\d|2[0-3]):([0-5]\d)$/);
    if (!match) return null;
    return {
      hour: Number(match[1]),
      minute: Number(match[2]),
    };
  };

  // Calendar Actions
  const toggleDate = (date: Date) => {
    const key = toDateKey(date);
    setSelectedDates((prev) => {
      if (prev.includes(key)) {
        return prev.filter((d) => d !== key);
      } else {
        return [...prev, key].sort();
      }
    });
  };

  const removeDateKey = (key: string) => {
    setSelectedDates((prev) => prev.filter((d) => d !== key));
  };

  const changeMonth = (direction: number) => {
    const next = new Date(calendarMonth);
    next.setMonth(next.getMonth() + direction);
    setCalendarMonth(next);
  };

  const goToToday = () => {
    const now = new Date();
    setCalendarMonth(new Date(now.getFullYear(), now.getMonth(), 1));
    if (!selectedDates.includes(todayKey)) {
      setSelectedDates((prev) => [...prev, todayKey].sort());
    }
  };

  // Preset Handlers
  const handleSelectToday = () => {
    setSelectedDates([todayKey]);
    setCalendarMonth(new Date(today.getFullYear(), today.getMonth(), 1));
  };

  const handleSelectTomorrow = () => {
    const tmr = new Date(today);
    tmr.setDate(tmr.getDate() + 1);
    setSelectedDates([toDateKey(tmr)]);
    setCalendarMonth(new Date(tmr.getFullYear(), tmr.getMonth(), 1));
  };

  const handleSelectNext3Days = () => {
    const keys: string[] = [];
    for (let i = 0; i < 3; i++) {
      const d = new Date(today);
      d.setDate(d.getDate() + i);
      keys.push(toDateKey(d));
    }
    setSelectedDates(keys);
  };

  const handleSelectCurrentWeek = () => {
    const start = startOfWeek(today);
    const keys: string[] = [];
    for (let i = 0; i < 7; i++) {
      const d = new Date(start);
      d.setDate(start.getDate() + i);
      keys.push(toDateKey(d));
    }
    setSelectedDates(keys);
  };

  const handleSelectWorkdays = () => {
    const start = startOfWeek(today);
    const keys: string[] = [];
    for (let i = 0; i < 5; i++) {
      const d = new Date(start);
      d.setDate(start.getDate() + i);
      keys.push(toDateKey(d));
    }
    setSelectedDates(keys);
  };

  const handleClearDates = () => {
    setSelectedDates([]);
  };

  // Month Grid Days (42 cells)
  const monthDays = useMemo(() => {
    const monthStart = new Date(calendarMonth.getFullYear(), calendarMonth.getMonth(), 1);
    const gridStart = startOfWeek(monthStart);
    return Array.from({ length: 42 }, (_, index) => {
      const d = new Date(gridStart);
      d.setDate(gridStart.getDate() + index);
      return d;
    });
  }, [calendarMonth]);

  const handleSave = async () => {
    if (loading) return;

    if (!title.trim()) {
      Alert.alert('Lỗi', taskType === 'Task' ? 'Vui lòng nhập tiêu đề công việc.' : 'Vui lòng nhập tiêu đề lịch trình.');
      return;
    }

    if (selectedDates.length === 0) {
      Alert.alert('Lỗi', 'Vui lòng chọn ít nhất một ngày trên lịch.');
      return;
    }

    const start = taskType === 'Schedule' ? parseTime(startTime) : null;
    if (taskType === 'Schedule' && !start) {
      Alert.alert('Lỗi', 'Giờ bắt đầu cần nhập theo dạng HH:mm.');
      return;
    }

    const end = parseTime(endTime);
    if (!end) {
      Alert.alert('Lỗi', taskType === 'Task' ? 'Giờ hoàn thành cần nhập theo dạng HH:mm.' : 'Giờ kết thúc cần nhập theo dạng HH:mm.');
      return;
    }

    if (taskType === 'Schedule' && start && end) {
      const startMinutes = start.hour * 60 + start.minute;
      const endMinutes = end.hour * 60 + end.minute;
      if (endMinutes <= startMinutes) {
        Alert.alert('Lỗi', 'Giờ kết thúc lịch trình phải sau giờ bắt đầu.');
        return;
      }
    }

    const now = new Date();
    const sortedDateKeys = [...selectedDates].sort();
    const payloads: CreateTaskPayload[] = [];

    for (const key of sortedDateKeys) {
      const date = parseDateKey(key);
      const startAt = taskType === 'Schedule' && start
        ? new Date(date.getFullYear(), date.getMonth(), date.getDate(), start.hour, start.minute, 0)
        : new Date(date.getFullYear(), date.getMonth(), date.getDate(), 0, 0, 0);

      const endAt = new Date(date.getFullYear(), date.getMonth(), date.getDate(), end.hour, end.minute, 0);

      payloads.push({
        title: title.trim(),
        description: description.trim() || undefined,
        startAt: toLocalIsoString(startAt),
        endAt: toLocalIsoString(endAt),
        dueAt: toLocalIsoString(endAt),
        priority: taskType === 'Task' ? priority : 'Normal',
        repeatType: taskType === 'Task' ? repeatType : 'None',
        reminderBeforeMinutes,
        taskType,
        location: taskType === 'Schedule' && location.trim() ? location.trim() : undefined,
      });
    }

    setLoading(true);
    try {
      if (payloads.length === 1) {
        await taskService.createTask(payloads[0]);
      } else {
        await taskService.createBatchTasks(payloads);
      }

      const successMsg = payloads.length > 1
        ? (taskType === 'Task' ? `Đã thêm công việc vào ${payloads.length} ngày đã chọn.` : `Đã thêm lịch trình vào ${payloads.length} ngày đã chọn.`)
        : (taskType === 'Task' ? 'Đã thêm công việc mới.' : 'Đã thêm lịch trình mới.');

      if (Platform.OS === 'web') {
        window.alert(successMsg);
        navigation.goBack();
      } else {
        Alert.alert('Thành công', successMsg, [
          { text: 'OK', onPress: () => navigation.goBack() },
        ]);
        // Tự động quay lại sau khi người dùng bấm OK
      }
    } catch (e: any) {
      Alert.alert(
        'Lỗi',
        e.response?.data?.message || e.message || (taskType === 'Task' ? 'Không thể tạo công việc.' : 'Không thể tạo lịch trình.')
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <PageContainer>
      <ScrollView contentContainerStyle={styles.scrollContainer} keyboardShouldPersistTaps="handled">
        <AppCard style={styles.card}>
          {/* Segment Type Selection */}
          <View style={styles.segmentGroup}>
            <TouchableOpacity
              style={[styles.segmentButton, taskType === 'Task' && styles.segmentActive]}
              onPress={() => setTaskType('Task')}
            >
              <Ionicons
                name={taskType === 'Task' ? 'checkbox' : 'checkbox-outline'}
                size={16}
                color={taskType === 'Task' ? '#FFFFFF' : COLORS.textMuted}
                style={styles.segmentIcon}
              />
              <Text style={[styles.segmentText, taskType === 'Task' && styles.segmentTextActive]}>Công việc</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={[styles.segmentButton, taskType === 'Schedule' && styles.segmentActive]}
              onPress={() => setTaskType('Schedule')}
            >
              <Ionicons
                name={taskType === 'Schedule' ? 'calendar' : 'calendar-outline'}
                size={16}
                color={taskType === 'Schedule' ? '#FFFFFF' : COLORS.textMuted}
                style={styles.segmentIcon}
              />
              <Text style={[styles.segmentText, taskType === 'Schedule' && styles.segmentTextActive]}>Lịch trình</Text>
            </TouchableOpacity>
          </View>

          {/* Title */}
          <AppInput
            label={taskType === 'Task' ? 'Tiêu đề công việc' : 'Tiêu đề lịch trình'}
            placeholder={taskType === 'Task' ? 'Nhập tên công việc' : 'Nhập tên lịch trình'}
            value={title}
            onChangeText={setTitle}
          />

          {/* Location for Schedule */}
          {taskType === 'Schedule' && (
            <AppInput
              label="Địa điểm diễn ra (Không bắt buộc)"
              placeholder="Nhập địa điểm nếu có"
              value={location}
              onChangeText={setLocation}
            />
          )}

          {/* Description */}
          <AppInput
            label={taskType === 'Task' ? 'Mô tả chi tiết (Không bắt buộc)' : 'Ghi chú lịch trình (Không bắt buộc)'}
            placeholder={taskType === 'Task' ? 'Thêm mô tả nếu cần' : 'Thêm ghi chú/mô tả nếu cần'}
            value={description}
            onChangeText={setDescription}
            multiline
            numberOfLines={3}
            style={{ height: 75, textAlignVertical: 'top' }}
          />

          {/* CALENDAR SECTION */}
          <View style={styles.calendarSectionHeader}>
            <View style={styles.sectionTitleRow}>
              <Ionicons name="calendar-outline" size={18} color={COLORS.primary} />
              <Text style={styles.sectionLabel}>
                {taskType === 'Task' ? 'Chọn ngày thực hiện' : 'Chọn ngày diễn ra'}
              </Text>
            </View>
            <View style={styles.selectedCountBadge}>
              <Text style={styles.selectedCountBadgeText}>
                {selectedDates.length === 0 ? 'Chưa chọn ngày' : `Đã chọn: ${selectedDates.length} ngày`}
              </Text>
            </View>
          </View>

          {/* Quick Select Presets */}
          <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.presetScroll}>
            <TouchableOpacity style={styles.presetChip} onPress={handleSelectToday}>
              <Text style={styles.presetChipText}>Hôm nay</Text>
            </TouchableOpacity>
            <TouchableOpacity style={styles.presetChip} onPress={handleSelectTomorrow}>
              <Text style={styles.presetChipText}>Ngày mai</Text>
            </TouchableOpacity>
            <TouchableOpacity style={styles.presetChip} onPress={handleSelectNext3Days}>
              <Text style={styles.presetChipText}>3 ngày tới</Text>
            </TouchableOpacity>
            <TouchableOpacity style={styles.presetChip} onPress={handleSelectCurrentWeek}>
              <Text style={styles.presetChipText}>Cả tuần này</Text>
            </TouchableOpacity>
            <TouchableOpacity style={styles.presetChip} onPress={handleSelectWorkdays}>
              <Text style={styles.presetChipText}>T2 - T6</Text>
            </TouchableOpacity>
            {selectedDates.length > 0 && (
              <TouchableOpacity style={[styles.presetChip, styles.clearPresetChip]} onPress={handleClearDates}>
                <Ionicons name="close-circle-outline" size={14} color="#EF4444" style={{ marginRight: 4 }} />
                <Text style={styles.clearPresetChipText}>Xóa chọn</Text>
              </TouchableOpacity>
            )}
          </ScrollView>

          {/* Calendar Card Component */}
          <View style={styles.calendarCard}>
            {/* Month / Year Navigator */}
            <View style={styles.monthNavRow}>
              <TouchableOpacity style={styles.navButton} onPress={() => changeMonth(-1)}>
                <Ionicons name="chevron-back" size={20} color={COLORS.text} />
              </TouchableOpacity>
              <TouchableOpacity style={styles.monthTitleWrapper} onPress={goToToday}>
                <Text style={styles.monthTitleText}>
                  Tháng {calendarMonth.getMonth() + 1}, {calendarMonth.getFullYear()}
                </Text>
                <View style={styles.todayIndicatorPill}>
                  <Text style={styles.todayIndicatorText}>Về hôm nay</Text>
                </View>
              </TouchableOpacity>
              <TouchableOpacity style={styles.navButton} onPress={() => changeMonth(1)}>
                <Ionicons name="chevron-forward" size={20} color={COLORS.text} />
              </TouchableOpacity>
            </View>

            {/* Week Days Header */}
            <View style={styles.weekHeader}>
              {WEEK_DAYS.map((day, idx) => (
                <Text
                  key={day}
                  style={[
                    styles.weekHeaderText,
                    idx >= 5 && styles.weekendHeaderText,
                  ]}
                >
                  {day}
                </Text>
              ))}
            </View>

            {/* Month Day Grid */}
            <View style={styles.monthGrid}>
              {monthDays.map((date) => {
                const key = toDateKey(date);
                const isSelected = selectedDates.includes(key);
                const inMonth = date.getMonth() === calendarMonth.getMonth();
                const isCurrentToday = key === todayKey;

                return (
                  <TouchableOpacity
                    key={key}
                    activeOpacity={0.7}
                    style={[
                      styles.dayCell,
                      !inMonth && styles.mutedCell,
                      isCurrentToday && styles.todayCell,
                      isSelected && styles.selectedCell,
                    ]}
                    onPress={() => toggleDate(date)}
                  >
                    <Text
                      style={[
                        styles.dayNumber,
                        !inMonth && styles.mutedDayText,
                        isCurrentToday && !isSelected && styles.todayText,
                        isSelected && styles.selectedDayText,
                      ]}
                    >
                      {date.getDate()}
                    </Text>
                    {isSelected && (
                      <View style={styles.selectedDot}>
                        <Ionicons name="checkmark" size={10} color="#FFFFFF" />
                      </View>
                    )}
                  </TouchableOpacity>
                );
              })}
            </View>
          </View>

          {/* Selected Dates Chips */}
          {selectedDates.length > 0 && (
            <View style={styles.selectedDatesContainer}>
              <Text style={styles.selectedDatesSubLabel}>Các ngày đã chọn ({selectedDates.length}):</Text>
              <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.chipsScroll}>
                {selectedDates.map((key) => {
                  const d = parseDateKey(key);
                  return (
                    <View key={key} style={styles.dateChip}>
                      <Text style={styles.dateChipText}>{formatDateDisplay(d)}</Text>
                      <TouchableOpacity
                        hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
                        onPress={() => removeDateKey(key)}
                        style={styles.dateChipRemove}
                      >
                        <Ionicons name="close" size={13} color={COLORS.primary} />
                      </TouchableOpacity>
                    </View>
                  );
                })}
              </ScrollView>
            </View>
          )}

          {/* Time Pickers */}
          <Text style={styles.sectionLabel}>
            {taskType === 'Task' ? 'Giờ hoàn thành' : 'Khung giờ diễn ra'}
          </Text>
          <View style={styles.timeRow}>
            {taskType === 'Schedule' && (
              <View style={styles.timeColumn}>
                <AppInput
                  label="Giờ bắt đầu (HH:mm)"
                  placeholder="08:00"
                  keyboardType="numeric"
                  value={startTime}
                  onChangeText={(value) => setStartTime(formatTimeText(value))}
                  maxLength={5}
                />
              </View>
            )}
            <View style={styles.timeColumn}>
              <AppInput
                label={taskType === 'Task' ? 'Giờ hoàn thành (HH:mm)' : 'Giờ kết thúc (HH:mm)'}
                placeholder="09:00"
                keyboardType="numeric"
                value={endTime}
                onChangeText={(value) => setEndTime(formatTimeText(value))}
                maxLength={5}
              />
            </View>
          </View>

          {/* Reminders */}
          <Text style={styles.sectionLabel}>
            {taskType === 'Task' ? 'Nhắc nhở trước giờ hoàn thành' : 'Nhắc nhở trước giờ diễn ra'}
          </Text>
          <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.scrollChoiceRow}>
            <TouchableOpacity
              style={[styles.scrollChoiceBtn, reminderBeforeMinutes === undefined && !isCustomActive && styles.activeChoiceBtn]}
              onPress={() => {
                setReminderBeforeMinutes(undefined);
                setIsCustomActive(false);
              }}
            >
              <Text style={[styles.choiceBtnText, reminderBeforeMinutes === undefined && !isCustomActive && styles.activeChoiceBtnText]}>
                Không nhắc
              </Text>
            </TouchableOpacity>

            <TouchableOpacity
              style={[styles.scrollChoiceBtn, reminderBeforeMinutes === 15 && !isCustomActive && styles.activeChoiceBtn]}
              onPress={() => {
                setReminderBeforeMinutes(15);
                setIsCustomActive(false);
              }}
            >
              <Text style={[styles.choiceBtnText, reminderBeforeMinutes === 15 && !isCustomActive && styles.activeChoiceBtnText]}>
                15 phút
              </Text>
            </TouchableOpacity>

            <TouchableOpacity
              style={[styles.scrollChoiceBtn, reminderBeforeMinutes === 30 && !isCustomActive && styles.activeChoiceBtn]}
              onPress={() => {
                setReminderBeforeMinutes(30);
                setIsCustomActive(false);
              }}
            >
              <Text style={[styles.choiceBtnText, reminderBeforeMinutes === 30 && !isCustomActive && styles.activeChoiceBtnText]}>
                30 phút
              </Text>
            </TouchableOpacity>

            <TouchableOpacity
              style={[styles.scrollChoiceBtn, reminderBeforeMinutes === 60 && !isCustomActive && styles.activeChoiceBtn]}
              onPress={() => {
                setReminderBeforeMinutes(60);
                setIsCustomActive(false);
              }}
            >
              <Text style={[styles.choiceBtnText, reminderBeforeMinutes === 60 && !isCustomActive && styles.activeChoiceBtnText]}>
                1 tiếng
              </Text>
            </TouchableOpacity>

            <TouchableOpacity
              style={[styles.scrollChoiceBtn, isCustomActive && styles.activeChoiceBtn]}
              onPress={() => {
                setIsCustomActive(true);
                setReminderBeforeMinutes(customReminder ? Number(customReminder) : undefined);
              }}
            >
              <Text style={[styles.choiceBtnText, isCustomActive && styles.activeChoiceBtnText]}>
                Tự chọn
              </Text>
            </TouchableOpacity>
          </ScrollView>

          {isCustomActive && (
            <AppInput
              label="Số phút nhắc trước"
              placeholder="Ví dụ: 8"
              keyboardType="numeric"
              value={customReminder}
              onChangeText={(value) => {
                const cleaned = value.replace(/\D/g, '');
                setCustomReminder(cleaned);
                setReminderBeforeMinutes(cleaned ? Number(cleaned) : undefined);
              }}
            />
          )}

          {/* Task-specific Priority and Repeat */}
          {taskType === 'Task' && (
            <>
              <Text style={styles.sectionLabel}>Độ ưu tiên</Text>
              <View style={styles.choiceRow}>
                {(['Low', 'Normal', 'High'] as const).map((p) => {
                  const isSelected = priority === p;
                  const labelMap = { Low: 'Thấp', Normal: 'Vừa', High: 'Cao' };
                  const colorMap = { Low: '#10B981', Normal: '#3B82F6', High: '#EF4444' };
                  return (
                    <TouchableOpacity
                      key={p}
                      style={[
                        styles.choiceBtn,
                        isSelected && { borderColor: colorMap[p], backgroundColor: `${colorMap[p]}12` },
                      ]}
                      onPress={() => setPriority(p)}
                    >
                      <Text style={[styles.choiceBtnText, isSelected && { color: colorMap[p], fontWeight: '700' }]}>
                        {labelMap[p]}
                      </Text>
                    </TouchableOpacity>
                  );
                })}
              </View>

              <Text style={styles.sectionLabel}>Lặp lại</Text>
              <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.scrollChoiceRow}>
                {(['None', 'Daily', 'Weekly', 'Monthly'] as const).map((r) => {
                  const isSelected = repeatType === r;
                  const labelMap = { None: 'Không', Daily: 'Mỗi ngày', Weekly: 'Mỗi tuần', Monthly: 'Mỗi tháng' };
                  return (
                    <TouchableOpacity
                      key={r}
                      style={[styles.scrollChoiceBtn, isSelected && styles.activeChoiceBtn]}
                      onPress={() => setRepeatType(r)}
                    >
                      <Text style={[styles.choiceBtnText, isSelected && styles.activeChoiceBtnText]}>
                        {labelMap[r]}
                      </Text>
                    </TouchableOpacity>
                  );
                })}
              </ScrollView>
            </>
          )}

          {/* Submit Button */}
          <AppButton
            title={
              selectedDates.length > 1
                ? `${taskType === 'Task' ? 'Tạo công việc' : 'Tạo lịch trình'} (${selectedDates.length} ngày)`
                : taskType === 'Task' ? 'Tạo công việc' : 'Tạo lịch trình'
            }
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
  },
  scrollContainer: {
    padding: 16,
    paddingBottom: 36,
  },
  card: {
    padding: 18,
    marginBottom: 32,
  },
  segmentGroup: {
    flexDirection: 'row',
    backgroundColor: COLORS.cardBgLight,
    borderRadius: 10,
    padding: 4,
    marginBottom: 16,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
  },
  segmentButton: {
    flex: 1,
    minHeight: 38,
    flexDirection: 'row',
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
  },
  segmentIcon: {
    marginRight: 6,
  },
  segmentActive: {
    backgroundColor: COLORS.primary,
    shadowColor: COLORS.primary,
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.25,
    shadowRadius: 4,
    elevation: 3,
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
  calendarSectionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: 14,
    marginBottom: 8,
  },
  sectionTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  sectionLabel: {
    fontSize: 14,
    fontWeight: '700',
    color: COLORS.text,
    fontFamily: 'System',
    marginTop: 12,
    marginBottom: 10,
  },
  selectedCountBadge: {
    backgroundColor: 'rgba(184, 77, 53, 0.12)',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: 'rgba(184, 77, 53, 0.25)',
  },
  selectedCountBadgeText: {
    fontSize: 11,
    fontWeight: '700',
    color: COLORS.primary,
  },
  presetScroll: {
    flexDirection: 'row',
    marginBottom: 10,
  },
  presetChip: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    backgroundColor: COLORS.cardBgLight,
    borderRadius: 16,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    marginRight: 8,
    flexDirection: 'row',
    alignItems: 'center',
  },
  presetChipText: {
    fontSize: 12,
    fontWeight: '600',
    color: COLORS.text,
  },
  clearPresetChip: {
    borderColor: 'rgba(239, 68, 68, 0.3)',
    backgroundColor: 'rgba(239, 68, 68, 0.08)',
  },
  clearPresetChipText: {
    fontSize: 12,
    fontWeight: '600',
    color: '#EF4444',
  },
  calendarCard: {
    backgroundColor: COLORS.cardBgLight,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    padding: 12,
    marginBottom: 12,
  },
  monthNavRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 12,
    paddingHorizontal: 4,
  },
  navButton: {
    width: 34,
    height: 34,
    borderRadius: 17,
    backgroundColor: COLORS.cardBg,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    alignItems: 'center',
    justifyContent: 'center',
  },
  monthTitleWrapper: {
    alignItems: 'center',
  },
  monthTitleText: {
    fontSize: 15,
    fontWeight: '800',
    color: COLORS.text,
    fontFamily: 'System',
  },
  todayIndicatorPill: {
    marginTop: 2,
    paddingHorizontal: 6,
    paddingVertical: 1,
    borderRadius: 6,
    backgroundColor: 'rgba(184, 77, 53, 0.08)',
  },
  todayIndicatorText: {
    fontSize: 10,
    fontWeight: '600',
    color: COLORS.primary,
  },
  weekHeader: {
    flexDirection: 'row',
    marginBottom: 6,
    borderBottomWidth: 1,
    borderBottomColor: COLORS.cardBorder,
    paddingBottom: 6,
  },
  weekHeaderText: {
    width: '14.2857%',
    textAlign: 'center',
    fontSize: 11,
    fontWeight: '700',
    color: COLORS.textMuted,
    fontFamily: 'System',
  },
  weekendHeaderText: {
    color: COLORS.primary,
    opacity: 0.85,
  },
  monthGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
  },
  dayCell: {
    width: '14.2857%',
    height: 44,
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
    marginVertical: 2,
    position: 'relative',
  },
  mutedCell: {
    opacity: 0.35,
  },
  todayCell: {
    borderWidth: 1.5,
    borderColor: COLORS.accent,
  },
  selectedCell: {
    backgroundColor: COLORS.primary,
    shadowColor: COLORS.primary,
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.3,
    shadowRadius: 2,
    elevation: 2,
  },
  dayNumber: {
    fontSize: 13,
    fontWeight: '700',
    color: COLORS.text,
    fontFamily: 'System',
  },
  mutedDayText: {
    color: COLORS.textMuted,
  },
  todayText: {
    color: COLORS.accent,
    fontWeight: '800',
  },
  selectedDayText: {
    color: '#FFFFFF',
    fontWeight: '800',
  },
  selectedDot: {
    position: 'absolute',
    bottom: 2,
    width: 12,
    height: 12,
    borderRadius: 6,
    alignItems: 'center',
    justifyContent: 'center',
  },
  selectedDatesContainer: {
    marginBottom: 12,
  },
  selectedDatesSubLabel: {
    fontSize: 12,
    fontWeight: '600',
    color: COLORS.textMuted,
    marginBottom: 6,
  },
  chipsScroll: {
    flexDirection: 'row',
  },
  dateChip: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(184, 77, 53, 0.1)',
    borderWidth: 1,
    borderColor: 'rgba(184, 77, 53, 0.25)',
    borderRadius: 8,
    paddingHorizontal: 8,
    paddingVertical: 5,
    marginRight: 8,
  },
  dateChipText: {
    fontSize: 12,
    fontWeight: '700',
    color: COLORS.primary,
    marginRight: 6,
  },
  dateChipRemove: {
    width: 16,
    height: 16,
    borderRadius: 8,
    backgroundColor: 'rgba(184, 77, 53, 0.15)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  timeRow: {
    flexDirection: 'row',
    gap: 12,
  },
  timeColumn: {
    flex: 1,
    minWidth: 0,
  },
  choiceRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 8,
  },
  choiceBtn: {
    flex: 1,
    minHeight: 42,
    marginHorizontal: 4,
    paddingVertical: 10,
    borderRadius: 8,
    borderWidth: 1.5,
    borderColor: COLORS.cardBorder,
    backgroundColor: COLORS.cardBgLight,
    alignItems: 'center',
    justifyContent: 'center',
  },
  activeChoiceBtn: {
    borderColor: COLORS.primary,
    backgroundColor: 'rgba(184, 77, 53, 0.1)',
  },
  choiceBtnText: {
    fontSize: 13,
    fontWeight: '600',
    color: COLORS.text,
    fontFamily: 'System',
    textAlign: 'center',
  },
  activeChoiceBtnText: {
    color: COLORS.primary,
  },
  scrollChoiceRow: {
    flexDirection: 'row',
    marginBottom: 8,
  },
  scrollChoiceBtn: {
    minHeight: 42,
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: 8,
    borderWidth: 1.5,
    borderColor: COLORS.cardBorder,
    backgroundColor: COLORS.cardBgLight,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 8,
  },
  saveButton: {
    marginTop: 20,
  },
});

export default AddTaskScreen;

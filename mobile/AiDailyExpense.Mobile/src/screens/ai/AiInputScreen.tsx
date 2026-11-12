import React, { useEffect, useRef, useState } from 'react';
import { View, Text, StyleSheet, ScrollView, TextInput, Alert, ActivityIndicator, TouchableOpacity } from 'react-native';
import { Ionicons, MaterialCommunityIcons } from '@expo/vector-icons';
import * as ImagePicker from 'expo-image-picker';
import aiService, { AiParseResponseData, AiTimetableImageResponseData } from '../../services/aiService';
import AppButton from '../../components/AppButton';
import AppCard from '../../components/AppCard';
import { formatMoney } from '../../utils/formatMoney';
import { formatDate } from '../../utils/formatDate';
import { syncAllPendingTasksNotifications } from '../../utils/notificationHelper';
import PageContainer from '../../components/PageContainer';
import { COLORS } from '../../utils/theme';
import { getApiErrorMessage } from '../../utils/apiError';

declare const require: any;

const formatTaskScheduleTime = (task: { startAt?: string; endAt?: string; dueAt?: string; taskType?: string }) => {
  if (task.startAt && task.endAt) {
    const end = new Date(task.endAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    return `${formatDate(task.startAt)} - ${end}`;
  }

  if (task.startAt) {
    return formatDate(task.startAt);
  }

  if (task.dueAt) {
    return `${task.taskType === 'Schedule' ? 'Thời gian' : 'Hạn'}: ${formatDate(task.dueAt)}`;
  }

  return 'Không hẹn ngày';
};

const AiInputScreen = ({ route, navigation }: any) => {
  const [text, setText] = useState('');
  const [loading, setLoading] = useState(false);
  const [imageLoading, setImageLoading] = useState(false);
  const [recognizing, setRecognizing] = useState(false);
  const [result, setResult] = useState<AiParseResponseData | null>(null);
  const [timetableResult, setTimetableResult] = useState<AiTimetableImageResponseData | null>(null);
  const speechBaseText = useRef('');
  const speechModuleRef = useRef<any>(null);
  const speechSubscriptionsRef = useRef<Array<{ remove: () => void }>>([]);
  const inputRef = useRef<TextInput>(null);

  const clearSpeechSubscriptions = () => {
    speechSubscriptionsRef.current.forEach((subscription) => subscription.remove());
    speechSubscriptionsRef.current = [];
  };

  const getSpeechModule = () => {
    if (speechModuleRef.current) return speechModuleRef.current;

    try {
      const speechRecognition = require('expo-speech-recognition');
      speechModuleRef.current = speechRecognition.ExpoSpeechRecognitionModule;
      return speechModuleRef.current;
    } catch {
      return null;
    }
  };

  const registerSpeechListeners = (speechModule: any) => {
    clearSpeechSubscriptions();
    speechSubscriptionsRef.current = [
      speechModule.addListener('start', () => {
        setRecognizing(true);
      }),
      speechModule.addListener('end', () => {
        setRecognizing(false);
      }),
      speechModule.addListener('result', (event: any) => {
        const transcript = event.results[0]?.transcript?.trim();
        if (!transcript) return;

        const base = speechBaseText.current;
        setText(base ? `${base} ${transcript}` : transcript);
      }),
      speechModule.addListener('error', (event: any) => {
        setRecognizing(false);
        Alert.alert('Giong noi', event.message || 'Khong the nhan giong noi luc nay.');
      }),
    ];
  };

  useEffect(() => {
    return () => {
      speechModuleRef.current?.abort?.();
      clearSpeechSubscriptions();
    };
  }, []);

  useEffect(() => {
    const mode = route?.params?.mode;
    if (mode === 'voice') {
      navigation.setParams({ mode: undefined });
      setTimeout(() => {
        handleVoiceInput();
      }, 100);
    } else if (mode === 'text') {
      navigation.setParams({ mode: undefined });
      setTimeout(() => {
        inputRef.current?.focus();
      }, 100);
    }
  }, [route?.params?.mode]);

  const handleVoiceInput = async () => {
    if (recognizing) {
      speechModuleRef.current?.stop?.();
      return;
    }

    try {
      const speechModule = getSpeechModule();
      if (!speechModule) {
        Alert.alert('Giong noi', 'Ban can build lai APK/dev build moi de dung nhap bang giong noi.');
        return;
      }

      registerSpeechListeners(speechModule);

      if (!speechModule.isRecognitionAvailable()) {
        Alert.alert('Giong noi', 'Thiet bi chua san sang nhan dien giong noi luc nay.');
        return;
      }

      const permission = await speechModule.requestPermissionsAsync();
      if (!permission.granted) {
        Alert.alert('Giong noi', 'Ban can cho phep micro de nhap bang giong noi.');
        return;
      }

      speechBaseText.current = text.trim();
      speechModule.start({
        lang: 'vi-VN',
        interimResults: true,
        maxAlternatives: 1,
        continuous: false,
        addsPunctuation: true,
        contextualStrings: [
          'chi tieu',
          'cong viec',
          'an sang',
          'do xang',
          'mua hang',
          'ngay mai',
          'hom nay',
          'tra tien',
        ],
      });
    } catch (e: any) {
      setRecognizing(false);
      Alert.alert('Giong noi', e.message || 'Khong the bat nhan giong noi.');
    }
  };

  const handleSend = async () => {
    if (!text.trim()) {
      Alert.alert('Lỗi', 'Vui lòng nhập nội dung cần ghi nhận.');
      return;
    }

    setLoading(true);
    setResult(null);
    setTimetableResult(null);
    try {
      const res = await aiService.parseText(text.trim());
      if (res.success && res.data) {
        setResult(res.data);
        setText(''); // Clear the text input on successful save
        if (res.data.intent === 'add_task') {
          syncAllPendingTasksNotifications().catch((err) => console.error('Error syncing notifications:', err));
        }
      } else {
        Alert.alert('Lỗi', res.message || 'Không thể xử lý nội dung này.');
      }
    } catch (e: any) {
      const errorMsg = getApiErrorMessage(e, 'Lỗi khi gửi nội dung.');
      Alert.alert('Lỗi', errorMsg);
    } finally {
      setLoading(false);
    }
  };

  const handlePickTimetableImage = async () => {
    setImageLoading(true);
    setResult(null);
    setTimetableResult(null);
    try {
      const permission = await ImagePicker.requestMediaLibraryPermissionsAsync();
      if (!permission.granted) {
        Alert.alert('Ảnh thời khóa biểu', 'Bạn cần cho phép truy cập ảnh để AI đọc thời khóa biểu.');
        return;
      }

      const picked = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ImagePicker.MediaTypeOptions.Images,
        allowsEditing: false,
        quality: 0.85,
      });

      if (picked.canceled || !picked.assets?.[0]) {
        return;
      }

      const asset = picked.assets[0];
      const res = await aiService.parseTimetableImage({
        uri: asset.uri,
        name: asset.fileName || `timetable-${Date.now()}.jpg`,
        type: asset.mimeType || 'image/jpeg',
      });

      if (res.success && res.data) {
        setTimetableResult(res.data);
        if (res.data.savedTasks && res.data.savedTasks.length > 0) {
          syncAllPendingTasksNotifications().catch((err) => console.error('Error syncing notifications:', err));
        }
      } else {
        Alert.alert('Ảnh thời khóa biểu', res.message || 'Không thể đọc ảnh thời khóa biểu này.');
      }
    } catch (e: any) {
      const errorMsg = getApiErrorMessage(e, 'Lỗi khi gửi ảnh thời khóa biểu.');
      Alert.alert('Ảnh thời khóa biểu', errorMsg);
    } finally {
      setImageLoading(false);
    }
  };

  return (
    <PageContainer>
      <ScrollView contentContainerStyle={styles.scrollContainer} keyboardShouldPersistTaps="handled">
        <AppCard style={styles.card}>
          <View style={styles.headerRow}>
            <View style={styles.iconBox}>
              <Ionicons name="sparkles" size={20} color={COLORS.primary} />
            </View>
          <View style={styles.headerTextWrap}>
            <Text style={styles.title}>Ghi nhanh bằng AI</Text>
            <Text style={styles.desc}>Nhập chi tiêu hoặc công việc, hệ thống sẽ tự phân tích và lưu lại.</Text>
          </View>
        </View>

        <View style={styles.inputContainer}>
          <TextInput
            ref={inputRef}
            style={styles.textArea}
            placeholder="Nhập nội dung cần ghi nhận..."
            placeholderTextColor="#9CA3AF"
            multiline
            numberOfLines={5}
            value={text}
            onChangeText={setText}
          />
          {text.length > 0 && (
            <TouchableOpacity
              style={styles.clearButton}
              onPress={() => setText('')}
              activeOpacity={0.7}
            >
              <Ionicons name="close-circle" size={20} color="#9CA3AF" />
            </TouchableOpacity>
          )}
        </View>

        <TouchableOpacity
          style={[styles.voiceButton, recognizing ? styles.voiceButtonActive : null]}
          onPress={handleVoiceInput}
          activeOpacity={0.82}
        >
          <View style={[styles.voiceIconBox, recognizing ? styles.voiceIconBoxActive : null]}>
            <Ionicons name={recognizing ? 'stop' : 'mic-outline'} size={20} color={recognizing ? '#FFFFFF' : COLORS.primary} />
          </View>
          <View style={styles.voiceTextWrap}>
            <Text style={styles.voiceTitle}>{recognizing ? 'Đang nghe...' : 'Nhập bằng giọng nói'}</Text>
          </View>
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.imageButton}
          onPress={handlePickTimetableImage}
          activeOpacity={0.82}
          disabled={imageLoading}
        >
          <View style={styles.imageIconBox}>
            {imageLoading ? (
              <ActivityIndicator size="small" color="#0F766E" />
            ) : (
              <Ionicons name="image-outline" size={20} color="#0F766E" />
            )}
          </View>
          <View style={styles.voiceTextWrap}>
            <Text style={styles.voiceTitle}>{imageLoading ? 'Đang đọc ảnh...' : 'Đọc ảnh thời khóa biểu'}</Text>
          </View>
        </TouchableOpacity>

        <AppButton
          title="Phân tích và lưu"
          loading={loading}
          onPress={handleSend}
          style={styles.sendButton}
        />
      </AppCard>

      {loading ? (
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="small" color={COLORS.primary} />
          <Text style={styles.loadingText}>AI đang phân tích...</Text>
        </View>
      ) : null}

      {result ? (
        <AppCard style={styles.resultCard}>
          <View style={styles.resultHeader}>
            <Text style={styles.resultTitle}>Kết quả</Text>
            <View style={styles.intentBadge}>
              <Text style={styles.intentText}>
                {result.intent === 'add_expense'
                  ? (result.savedItems?.every(item => item.categoryType === 'Income')
                    ? 'Thu nhập'
                    : result.savedItems?.some(item => item.categoryType === 'Income')
                      ? 'Giao dịch'
                      : 'Chi tiêu')
                  : (result.savedTasks && result.savedTasks.length > 0
                    ? (result.savedTasks.some(t => t.taskType === 'Schedule')
                      ? (result.savedTasks.every(t => t.taskType === 'Schedule') ? 'Lịch trình' : 'Lịch & Việc')
                      : 'Công việc')
                    : (result.task?.taskType === 'Schedule' ? 'Lịch trình' : 'Công việc'))}
              </Text>
            </View>
          </View>

          {result.intent === 'add_expense' && result.savedItems && result.savedItems.length > 0 ? (
            <View>
              {result.savedItems.map((item, idx) => {
                const isIncome = item.categoryType === 'Income';
                return (
                  <View key={idx} style={styles.parsedItemRow}>
                    <View style={styles.itemIcon}>
                      <MaterialCommunityIcons 
                        name={isIncome ? 'cash-plus' : 'cash-minus'} 
                        size={18} 
                        color={isIncome ? '#22C55E' : '#EF4444'} 
                      />
                    </View>
                    <View style={styles.parsedItemDetails}>
                      <Text style={styles.parsedItemTitle} numberOfLines={1}>{item.title}</Text>
                      <Text style={styles.parsedItemCategory}>
                        {item.categoryName} · {item.paymentMethod === 'Transfer' ? 'Chuyển khoản' : 'Tiền mặt'}
                      </Text>
                    </View>
                    <Text 
                      style={[styles.parsedItemAmount, isIncome && { color: '#22C55E' }]} 
                      numberOfLines={1} 
                      adjustsFontSizeToFit
                    >
                      {isIncome ? '+' : '-'}{formatMoney(item.amount)}
                    </Text>
                  </View>
                );
              })}
            </View>
          ) : null}

          {result.intent === 'add_task' && (
            (result.savedTasks && result.savedTasks.length > 0) ? (
              <View>
                {result.savedTasks.map((task, idx) => (
                  <View key={idx} style={styles.parsedItemRow}>
                    <View style={styles.itemIcon}>
                      <Ionicons 
                        name={task.taskType === 'Schedule' ? "calendar-outline" : "calendar"} 
                        size={18} 
                        color={task.taskType === 'Schedule' ? COLORS.secondary : COLORS.primary} 
                      />
                    </View>
                    <View style={styles.parsedItemDetails}>
                      <Text style={styles.parsedItemTitle} numberOfLines={1}>{task.title}</Text>
                      <Text style={styles.parsedItemCategory}>
                        {formatTaskScheduleTime(task)}
                      </Text>
                      {task.location ? (
                        <Text style={styles.parsedItemCategory} numberOfLines={1}>
                          📍 {task.location}
                        </Text>
                      ) : null}
                      {task.description ? (
                        <Text style={styles.parsedItemCategory} numberOfLines={2}>
                          📝 {task.description}
                        </Text>
                      ) : null}
                    </View>
                    <Text style={[styles.parsedTaskStatus, task.taskType === 'Schedule' && { color: COLORS.secondary }]}>
                      {task.taskType === 'Schedule' 
                        ? 'Đã lên lịch' 
                        : (task.status === 'Pending' ? 'Đang chờ' : 'Đã xong')}
                    </Text>
                  </View>
                ))}
              </View>
            ) : (result.task ? (
              <View style={styles.parsedItemRow}>
                <View style={styles.itemIcon}>
                  <Ionicons 
                    name={result.task.taskType === 'Schedule' ? "calendar-outline" : "calendar"} 
                    size={18} 
                    color={result.task.taskType === 'Schedule' ? COLORS.secondary : COLORS.primary} 
                  />
                </View>
                <View style={styles.parsedItemDetails}>
                  <Text style={styles.parsedItemTitle} numberOfLines={1}>{result.task.title}</Text>
                  <Text style={styles.parsedItemCategory}>
                    {formatTaskScheduleTime(result.task)}
                  </Text>
                  {result.task.location ? (
                    <Text style={styles.parsedItemCategory} numberOfLines={1}>
                      📍 {result.task.location}
                    </Text>
                  ) : null}
                  {result.task.description ? (
                    <Text style={styles.parsedItemCategory} numberOfLines={2}>
                      📝 {result.task.description}
                    </Text>
                  ) : null}
                </View>
                <Text style={[styles.parsedTaskStatus, result.task.taskType === 'Schedule' && { color: COLORS.secondary }]}>
                  {result.task.taskType === 'Schedule' 
                    ? 'Đã lên lịch' 
                    : (result.task.status === 'Pending' ? 'Đang chờ' : 'Đã xong')}
                </Text>
              </View>
            ) : null)
          )}
        </AppCard>
      ) : null}

      {timetableResult ? (
        <AppCard style={styles.resultCard}>
          <View style={styles.resultHeader}>
            <Text style={styles.resultTitle}>Thời khóa biểu</Text>
            <View style={styles.intentBadge}>
              <Text style={styles.intentText}>
                {timetableResult.detectedMode === 'week' ? 'Theo tuần' : timetableResult.detectedMode === 'day' ? 'Theo ngày' : 'Đã đọc'}
              </Text>
            </View>
          </View>

          {timetableResult.savedTasks.map((task) => (
            <View key={task.taskId} style={styles.parsedItemRow}>
              <View style={styles.itemIcon}>
                <Ionicons name="calendar-outline" size={18} color="#0F766E" />
              </View>
              <View style={styles.parsedItemDetails}>
                <Text style={styles.parsedItemTitle} numberOfLines={1}>{task.title}</Text>
                <Text style={styles.parsedItemCategory} numberOfLines={1}>
                  {task.startAt ? formatDate(task.startAt) : 'Đã đưa vào lịch'}
                  {task.endAt ? ` - ${new Date(task.endAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}` : ''}
                </Text>
              </View>
              <Text style={styles.parsedTaskStatus}>Đã lưu</Text>
            </View>
          ))}
        </AppCard>
      ) : null}

      <Text style={{ textAlign: 'center', color: '#94A3B8', fontSize: 11, marginVertical: 8 }}>
        Made by TÀI
      </Text>
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
  card: {
    padding: 18,
    marginBottom: 16,
  },
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 16,
  },
  iconBox: {
    width: 42,
    height: 42,
    borderRadius: 8,
    backgroundColor: COLORS.cardBgLight,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 12,
  },
  headerTextWrap: {
    flex: 1,
  },
  title: {
    fontSize: 17,
    fontWeight: '800',
    color: COLORS.text,
    fontFamily: 'System',
  },
  desc: {
    fontSize: 13,
    color: COLORS.textMuted,
    lineHeight: 18,
    marginTop: 3,
    fontFamily: 'System',
  },
  inputContainer: {
    position: 'relative',
    marginTop: 8,
  },
  clearButton: {
    position: 'absolute',
    top: 10,
    right: 10,
    zIndex: 10,
    padding: 4,
  },
  textArea: {
    minHeight: 120,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    borderRadius: 8,
    padding: 12,
    paddingRight: 36,
    fontSize: 15,
    color: COLORS.text,
    backgroundColor: COLORS.cardBgLight,
    fontFamily: 'System',
    textAlignVertical: 'top',
  },
  sendButton: {
    marginTop: 14,
  },
  voiceButton: {
    minHeight: 58,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    backgroundColor: COLORS.cardBgLight,
    flexDirection: 'row',
    alignItems: 'center',
    padding: 10,
    marginTop: 12,
  },
  voiceButtonActive: {
    borderColor: COLORS.primary,
    backgroundColor: 'rgba(184, 77, 53, 0.1)',
  },
  imageButton: {
    minHeight: 58,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    backgroundColor: COLORS.cardBgLight,
    flexDirection: 'row',
    alignItems: 'center',
    padding: 10,
    marginTop: 10,
  },
  imageIconBox: {
    width: 40,
    height: 40,
    borderRadius: 8,
    backgroundColor: 'rgba(76, 101, 143, 0.15)',
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 10,
  },
  voiceIconBox: {
    width: 40,
    height: 40,
    borderRadius: 8,
    backgroundColor: COLORS.cardBgLight,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 10,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
  },
  voiceIconBoxActive: {
    backgroundColor: COLORS.primary,
    borderColor: COLORS.primary,
  },
  voiceTextWrap: {
    flex: 1,
    minWidth: 0,
  },
  voiceTitle: {
    fontSize: 14,
    fontWeight: '800',
    color: COLORS.text,
    fontFamily: 'System',
  },
  voiceDesc: {
    fontSize: 12,
    color: COLORS.textMuted,
    lineHeight: 17,
    marginTop: 2,
    fontFamily: 'System',
  },
  loadingContainer: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 16,
  },
  loadingText: {
    fontSize: 13,
    color: COLORS.textMuted,
    fontFamily: 'System',
    marginTop: 8,
  },
  resultCard: {
    padding: 16,
    borderColor: COLORS.cardBorder,
    borderWidth: 1,
  },
  resultHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  resultTitle: {
    fontSize: 16,
    fontWeight: '800',
    color: COLORS.text,
    fontFamily: 'System',
  },
  intentBadge: {
    backgroundColor: COLORS.cardBgLight,
    paddingHorizontal: 9,
    paddingVertical: 5,
    borderRadius: 6,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
  },
  intentText: {
    fontSize: 12,
    fontWeight: '700',
    color: COLORS.primary,
    fontFamily: 'System',
  },
  parsedItemRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 11,
    borderBottomWidth: 1,
    borderBottomColor: COLORS.cardBorder,
  },
  itemIcon: {
    width: 34,
    height: 34,
    borderRadius: 8,
    backgroundColor: COLORS.cardBgLight,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 10,
  },
  parsedItemDetails: {
    flex: 1,
    minWidth: 0,
  },
  parsedItemTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: COLORS.text,
    fontFamily: 'System',
  },
  parsedItemCategory: {
    fontSize: 12,
    color: COLORS.textMuted,
    fontFamily: 'System',
    marginTop: 2,
  },
  parsedItemAmount: {
    maxWidth: 124,
    fontSize: 15,
    fontWeight: '800',
    color: '#EF4444',
    fontFamily: 'System',
    textAlign: 'right',
  },
  parsedTaskStatus: {
    fontSize: 12,
    fontWeight: '800',
    color: COLORS.primary,
    fontFamily: 'System',
  },
});

export default AiInputScreen;

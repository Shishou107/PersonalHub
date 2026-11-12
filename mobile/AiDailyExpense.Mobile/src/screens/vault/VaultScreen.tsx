import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet, ScrollView, Alert, TouchableOpacity } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import AppButton from '../../components/AppButton';
import AppCard from '../../components/AppCard';
import AppInput from '../../components/AppInput';
import Loading from '../../components/Loading';
import vaultService from '../../services/vaultService';
import { VaultCategory, VaultItem } from '../../types/vault';
import { useAuth } from '../../context/AuthContext';
import PageContainer from '../../components/PageContainer';
import { COLORS } from '../../utils/theme';

const VaultScreen = () => {
  const { user, refreshUser } = useAuth();
  const [items, setItems] = useState<VaultItem[]>([]);
  const [categories, setCategories] = useState<VaultCategory[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [title, setTitle] = useState('');
  const [websiteUrl, setWebsiteUrl] = useState('');
  const [login, setLogin] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [notes, setNotes] = useState('');
  const [oldPin, setOldPin] = useState('');
  const [pin, setPin] = useState('');
  const [confirmPin, setConfirmPin] = useState('');
  const [emailCode, setEmailCode] = useState('');
  const [unlockPin, setUnlockPin] = useState('');
  const [selectedItemId, setSelectedItemId] = useState<number | null>(null);
  const [revealedItem, setRevealedItem] = useState<{
    itemId: number;
    title: string;
    loginEmail?: string;
    loginUsername?: string;
    websiteUrl?: string;
    password: string;
    notes?: string;
  } | null>(null);

  const fetchData = async () => {
    setLoading(true);
    try {
      const [itemRes, catRes] = await Promise.all([
        vaultService.getItems(),
        vaultService.getCategories(),
      ]);
      if (itemRes.success && itemRes.data) setItems(itemRes.data);
      if (catRes.success && catRes.data) setCategories(catRes.data);
    } catch (e: any) {
      Alert.alert('Lỗi', e.response?.data?.message || e.message || 'Không thể tải kho tài khoản.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  const handleSetPin = async () => {
    const isChangePin = !!user?.hasVaultPin;
    if (isChangePin && !oldPin) {
      Alert.alert('Lỗi', 'Vui lòng nhập PIN cũ.');
      return;
    }
    if (isChangePin && (oldPin.length !== 6 || !/^\d+$/.test(oldPin))) {
      Alert.alert('Lỗi', 'PIN cũ phải gồm đúng 6 chữ số.');
      return;
    }
    if (!pin) {
      Alert.alert('Lỗi', isChangePin ? 'Vui lòng nhập PIN mới.' : 'Vui lòng nhập PIN.');
      return;
    }
    if (pin.length !== 6 || !/^\d+$/.test(pin)) {
      Alert.alert('Lỗi', isChangePin ? 'PIN mới phải gồm đúng 6 chữ số.' : 'PIN phải gồm đúng 6 chữ số.');
      return;
    }
    if (pin !== confirmPin) {
      Alert.alert('Lỗi', 'Xác nhận PIN mới không khớp.');
      return;
    }

    try {
      const payload = isChangePin ? { oldPin, pin, confirmPin } : { pin, confirmPin };
      const res = await vaultService.setPin(payload);
      if (res.success) {
        setOldPin('');
        setPin('');
        setConfirmPin('');
        await refreshUser();
        Alert.alert('Thành công', isChangePin ? 'Đã đổi PIN kho tài khoản thành công.' : 'Đã đặt PIN kho tài khoản thành công.');
      } else {
        Alert.alert('Lỗi', res.message || 'Không thể cập nhật PIN.');
      }
    } catch (e: any) {
      Alert.alert('Lỗi', e.response?.data?.message || e.message || 'Không thể đặt hoặc đổi PIN.');
    }
  };

  const handleCreate = async () => {
    if (!title.trim() || !password) {
      Alert.alert('Lỗi', 'Vui lòng nhập tên tài khoản và mật khẩu.');
      return;
    }
    if (password !== confirmPassword) {
      Alert.alert('Lỗi', 'Mật khẩu xác nhận không khớp.');
      return;
    }

    setSaving(true);
    try {
      await vaultService.createItem({
        vaultCategoryId: categories[0]?.vaultCategoryId,
        title: title.trim(),
        websiteUrl: websiteUrl.trim() || undefined,
        loginUsername: login.includes('@') ? undefined : login.trim() || undefined,
        loginEmail: login.includes('@') ? login.trim() : undefined,
        password,
        notes: notes.trim() || undefined,
        favorite: false,
      });
      setTitle('');
      setWebsiteUrl('');
      setLogin('');
      setPassword('');
      setConfirmPassword('');
      setNotes('');
      fetchData();
    } catch (e: any) {
      Alert.alert('Lỗi', e.response?.data?.message || e.message || 'Không thể lưu tài khoản.');
    } finally {
      setSaving(false);
    }
  };

  const handleRequestCode = async (id: number) => {
    try {
      await vaultService.requestRevealCode();
      setSelectedItemId(id);
      setEmailCode('');
      setUnlockPin('');
      setRevealedItem(null);
      Alert.alert('Đã gửi mã', 'Mã xác thực đã được gửi về email của bạn.');
    } catch (e: any) {
      Alert.alert('Lỗi', e.response?.data?.message || e.message || 'Không thể gửi mã email.');
    }
  };

  const handleReveal = async () => {
    if (!selectedItemId) return;
    if (!emailCode || !unlockPin) {
      Alert.alert('Lỗi', 'Vui lòng nhập đầy đủ mã email và PIN.');
      return;
    }

    try {
      const res = await vaultService.revealItem(selectedItemId, { emailCode, pin: unlockPin });
      const item = items.find((i) => i.vaultItemId === selectedItemId);
      if (res.success && res.data && item) {
        setRevealedItem({
          itemId: selectedItemId,
          title: item.title,
          loginEmail: item.loginEmail,
          loginUsername: item.loginUsername,
          websiteUrl: item.websiteUrl,
          password: res.data.password,
          notes: res.data.notes,
        });
      }
    } catch (e: any) {
      Alert.alert('Lỗi', e.response?.data?.message || e.message || 'Không thể mở mật khẩu.');
    }
  };

  const handleDelete = (item: VaultItem) => {
    Alert.alert('Xóa tài khoản', `Xóa "${item.title}" khỏi kho?`, [
      { text: 'Hủy', style: 'cancel' },
      {
        text: 'Xóa',
        style: 'destructive',
        onPress: async () => {
          await vaultService.deleteItem(item.vaultItemId);
          if (selectedItemId === item.vaultItemId) {
            setSelectedItemId(null);
            setRevealedItem(null);
            setEmailCode('');
            setUnlockPin('');
          }
          fetchData();
        },
      },
    ]);
  };

  if (loading) {
    return <Loading fullScreen message="Đang tải kho tài khoản..." />;
  }

  return (
    <PageContainer>
      <ScrollView contentContainerStyle={styles.scrollContainer} keyboardShouldPersistTaps="handled">
        <AppCard style={styles.card}>
          <View style={styles.headerRow}>
            <View style={styles.iconBox}>
              <Ionicons name="shield-checkmark-outline" size={21} color={COLORS.primary} />
            </View>
            <View style={styles.headerText}>
              <Text style={styles.title}>Kho tài khoản</Text>
              <Text style={styles.subTitle}>Mật khẩu được mã hóa, muốn xem cần email và PIN.</Text>
            </View>
          </View>

        <Text style={styles.sectionTitle}>{user?.hasVaultPin ? 'Đổi PIN bảo mật' : 'PIN bảo mật'}</Text>
        {user?.hasVaultPin && (
          <AppInput
            label="PIN cÅ©"
            placeholder="******"
            secureTextEntry
            keyboardType="numeric"
            value={oldPin}
            onChangeText={setOldPin}
            maxLength={6}
          />
        )}
        <View style={styles.twoCols}>
          <View style={styles.col}>
            <AppInput
              label={user?.hasVaultPin ? 'PIN mới' : 'PIN bảo mật'}
              placeholder="******"
              secureTextEntry
              keyboardType="numeric"
              value={pin}
              onChangeText={setPin}
              maxLength={6}
            />
          </View>
          <View style={styles.col}>
            <AppInput
              label={user?.hasVaultPin ? 'Nhập lại PIN mới' : 'Nhập lại PIN'}
              placeholder="******"
              secureTextEntry
              keyboardType="numeric"
              value={confirmPin}
              onChangeText={setConfirmPin}
              maxLength={6}
            />
          </View>
        </View>
        <AppButton title={user?.hasVaultPin ? 'Đổi PIN' : 'Đặt PIN'} variant="secondary" onPress={handleSetPin} />
      </AppCard>

      <AppCard style={styles.card}>
        <Text style={styles.sectionTitle}>Thêm tài khoản</Text>
        <AppInput label="Tên" placeholder="Gmail, Facebook, GitHub..." value={title} onChangeText={setTitle} />
        <AppInput label="Website" placeholder="https://..." value={websiteUrl} onChangeText={setWebsiteUrl} autoCapitalize="none" />
        <AppInput label="Email hoặc username" placeholder="Tài khoản đăng nhập" value={login} onChangeText={setLogin} autoCapitalize="none" />
        
        <View style={styles.twoCols}>
          <View style={styles.col}>
            <AppInput label="Mật khẩu" placeholder="Nhập mật khẩu" value={password} onChangeText={setPassword} secureTextEntry />
          </View>
          <View style={styles.col}>
            <AppInput label="Nhập lại mật khẩu" placeholder="Xác nhận mật khẩu" value={confirmPassword} onChangeText={setConfirmPassword} secureTextEntry />
          </View>
        </View>
        
        <AppInput label="Ghi chú" placeholder="Ghi chú nếu cần" value={notes} onChangeText={setNotes} multiline style={styles.notesInput} />
        <AppButton title="Lưu tài khoản" loading={saving} onPress={handleCreate} />
      </AppCard>

      <Text style={styles.listTitle}>Tài khoản đã lưu</Text>
      {items.length === 0 ? (
        <AppCard style={styles.emptyCard}>
          <Text style={styles.emptyText}>Chưa có tài khoản nào.</Text>
        </AppCard>
      ) : (
        items.map((item) => (
          <AppCard key={item.vaultItemId} style={styles.itemCard}>
            <View style={styles.itemHeader}>
              <View style={styles.itemIcon}>
                <Ionicons name="key-outline" size={18} color={COLORS.primary} />
              </View>
              <View style={styles.itemText}>
                <Text style={styles.itemTitle} numberOfLines={1}>{item.title}</Text>
              </View>
              <TouchableOpacity style={styles.iconBtn} onPress={() => handleRequestCode(item.vaultItemId)}>
                <Ionicons name="eye-outline" size={18} color={COLORS.secondary} />
              </TouchableOpacity>
              <TouchableOpacity style={styles.iconBtn} onPress={() => handleDelete(item)}>
                <Ionicons name="trash-outline" size={18} color="#EF4444" />
              </TouchableOpacity>
            </View>
          </AppCard>
        ))
      )}

      {selectedItemId ? (
        <AppCard style={styles.unlockCard}>
          <Text style={styles.sectionTitle}>Mở mật khẩu</Text>
          <AppInput label="Mã email" placeholder="6 số" keyboardType="numeric" value={emailCode} onChangeText={setEmailCode} maxLength={6} />
          <AppInput label="PIN" placeholder="Nhập PIN" secureTextEntry value={unlockPin} onChangeText={setUnlockPin} />
          <AppButton title="Xem mật khẩu" onPress={handleReveal} />
          
          {revealedItem ? (
            <View style={styles.revealedBox}>
              <Text style={styles.revealedTitle}>Thông tin chi tiết:</Text>
              
              <Text style={styles.revealedLabel}>Tên tài khoản:</Text>
              <Text style={styles.revealedValue}>{revealedItem.title}</Text>

              {(revealedItem.loginEmail || revealedItem.loginUsername) && (
                <>
                  <Text style={styles.revealedLabel}>Tài khoản đăng nhập:</Text>
                  <Text style={styles.revealedValue}>{revealedItem.loginEmail || revealedItem.loginUsername}</Text>
                </>
              )}

              {revealedItem.websiteUrl && (
                <>
                  <Text style={styles.revealedLabel}>Website:</Text>
                  <Text style={styles.revealedValue}>{revealedItem.websiteUrl}</Text>
                </>
              )}

              <Text style={styles.revealedLabel}>Mật khẩu:</Text>
              <Text style={styles.passwordText}>{revealedItem.password}</Text>

              {revealedItem.notes && (
                <>
                  <Text style={styles.revealedLabel}>Ghi chú:</Text>
                  <Text style={styles.revealedValue}>{revealedItem.notes}</Text>
                </>
              )}

              <AppButton 
                title="Ẩn thông tin" 
                variant="secondary" 
                style={{ marginTop: 12 }} 
                onPress={() => {
                  setRevealedItem(null);
                  setEmailCode('');
                  setUnlockPin('');
                  setSelectedItemId(null);
                }} 
              />
            </View>
          ) : null}
        </AppCard>
      ) : null}

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
    padding: 16,
    marginBottom: 14,
  },
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
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
  headerText: {
    flex: 1,
  },
  title: {
    fontSize: 18,
    fontWeight: '800',
    color: COLORS.text,
  },
  subTitle: {
    fontSize: 13,
    color: COLORS.textMuted,
    marginTop: 3,
    lineHeight: 18,
  },
  sectionTitle: {
    fontSize: 15,
    fontWeight: '800',
    color: COLORS.text,
    marginBottom: 10,
  },
  twoCols: {
    flexDirection: 'row',
    gap: 10,
  },
  col: {
    flex: 1,
    minWidth: 0,
  },
  notesInput: {
    minHeight: 64,
    textAlignVertical: 'top',
  },
  listTitle: {
    fontSize: 16,
    fontWeight: '800',
    color: COLORS.text,
    marginBottom: 10,
  },
  emptyCard: {
    padding: 20,
    alignItems: 'center',
  },
  emptyText: {
    color: COLORS.textMuted,
    fontWeight: '600',
  },
  itemCard: {
    padding: 12,
    marginBottom: 10,
  },
  itemHeader: {
    flexDirection: 'row',
    alignItems: 'center',
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
  itemText: {
    flex: 1,
    minWidth: 0,
  },
  itemTitle: {
    fontSize: 15,
    fontWeight: '800',
    color: COLORS.text,
  },
  iconBtn: {
    width: 34,
    height: 34,
    alignItems: 'center',
    justifyContent: 'center',
    marginLeft: 4,
  },
  unlockCard: {
    padding: 16,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
  },
  revealedBox: {
    marginTop: 14,
    padding: 12,
    borderRadius: 8,
    backgroundColor: COLORS.cardBgLight,
    borderLeftWidth: 3,
    borderLeftColor: COLORS.primary,
  },
  revealedTitle: {
    fontSize: 14,
    fontWeight: '800',
    color: COLORS.text,
    marginBottom: 8,
    fontFamily: 'System',
  },
  revealedLabel: {
    fontSize: 11,
    fontWeight: '700',
    color: COLORS.textMuted,
    marginTop: 8,
    textTransform: 'uppercase',
    fontFamily: 'System',
  },
  revealedValue: {
    fontSize: 14,
    fontWeight: '600',
    color: COLORS.text,
    marginTop: 2,
    fontFamily: 'System',
  },
  passwordText: {
    fontSize: 16,
    fontWeight: '800',
    color: '#EF4444',
    marginTop: 4,
    fontFamily: 'System',
  },
});
export default VaultScreen;

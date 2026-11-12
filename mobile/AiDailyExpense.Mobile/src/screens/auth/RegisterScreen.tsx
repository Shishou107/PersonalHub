import React, { useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  Alert,
  TouchableOpacity,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '../../context/AuthContext';
import AppInput from '../../components/AppInput';
import AppButton from '../../components/AppButton';
import AppCard from '../../components/AppCard';
import authService from '../../services/authService';
import PageContainer from '../../components/PageContainer';
import { COLORS } from '../../utils/theme';

const RegisterScreen = ({ navigation }: any) => {
  const { login } = useAuth();
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [errors, setErrors] = useState<{ fullName?: string; email?: string; password?: string; confirmPassword?: string }>({});

  const [isVerifying, setIsVerifying] = useState(false);
  const [verificationCode, setVerificationCode] = useState('');
  const [verifyingLoading, setVerifyingLoading] = useState(false);
  const [resendingCode, setResendingCode] = useState(false);

  const validate = () => {
    let valid = true;
    const newErrors: typeof errors = {};

    if (!fullName) {
      newErrors.fullName = 'Họ và tên là bắt buộc.';
      valid = false;
    }

    if (!email) {
      newErrors.email = 'Email là bắt buộc.';
      valid = false;
    } else if (!/\S+@\S+\.\S+/.test(email)) {
      newErrors.email = 'Email không hợp lệ.';
      valid = false;
    }

    if (!password) {
      newErrors.password = 'Mật khẩu là bắt buộc.';
      valid = false;
    } else if (password.length < 6) {
      newErrors.password = 'Mật khẩu phải từ 6 ký tự.';
      valid = false;
    }

    if (password !== confirmPassword) {
      newErrors.confirmPassword = 'Xác nhận mật khẩu không khớp.';
      valid = false;
    }

    setErrors(newErrors);
    return valid;
  };

  const handleRegister = async () => {
    if (!validate()) return;

    setLoading(true);
    try {
      const res = await authService.register({ fullName, email, password, confirmPassword });
      if (res.success) {
        setIsVerifying(true);
        Alert.alert('Đăng ký thành công', 'Một mã xác thực gồm 6 số đã được gửi đến email của bạn. Vui lòng nhập mã để hoàn tất đăng ký.');
      } else {
        Alert.alert('Đăng ký thất bại', res.message || 'Có lỗi xảy ra.');
      }
    } catch (e: any) {
      Alert.alert('Đăng ký thất bại', e.response?.data?.message || e.message || 'Có lỗi xảy ra trong quá trình đăng ký.');
    } finally {
      setLoading(false);
    }
  };

  const handleVerifyEmail = async () => {
    if (!/^\d{6}$/.test(verificationCode)) {
      Alert.alert('Lỗi', 'Mã xác thực phải gồm 6 số.');
      return;
    }

    setVerifyingLoading(true);
    try {
      const res = await authService.verifyEmail(email, verificationCode);
      if (res.success) {
        Alert.alert('Thành công', 'Email của bạn đã được xác thực thành công. Đang đăng nhập...', [
          {
            text: 'OK',
            onPress: async () => {
              try {
                await login(email, password);
              } catch (loginErr: any) {
                Alert.alert('Lỗi đăng nhập', loginErr.message || 'Không thể tự động đăng nhập.');
                navigation.navigate('Login');
              }
            }
          }
        ]);
      } else {
        Alert.alert('Lỗi', res.message || 'Mã xác thực không hợp lệ.');
      }
    } catch (e: any) {
      Alert.alert('Lỗi', e.response?.data?.message || e.message || 'Xác thực thất bại.');
    } finally {
      setVerifyingLoading(false);
    }
  };

  const handleResendCode = async () => {
    setResendingCode(true);
    try {
      await authService.requestEmailVerification(email);
      Alert.alert('Đã gửi lại', 'Mã xác thực mới đã được gửi về email của bạn.');
    } catch (e: any) {
      Alert.alert('Lỗi', e.response?.data?.message || e.message || 'Không thể gửi lại mã.');
    } finally {
      setResendingCode(false);
    }
  };

  return (
    <PageContainer>
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
        style={styles.container}
      >
      <ScrollView contentContainerStyle={styles.scrollContainer} keyboardShouldPersistTaps="handled">
        <View style={styles.headerContainer}>
          <View style={styles.logoMark}>
            <Ionicons name="person-add-outline" size={24} color={COLORS.primary} />
          </View>
          <Text style={styles.headerTitle}>Tạo tài khoản</Text>
          <Text style={styles.headerSub}>Bắt đầu ghi chép chi tiêu và lịch công việc</Text>
        </View>

        <AppCard style={styles.card}>
          {isVerifying ? (
            <>
              <Text style={styles.cardTitle}>Xác thực Email</Text>
              <Text style={styles.verifySub}>Mã xác thực đã được gửi đến email:</Text>
              <Text style={styles.emailText}>{email}</Text>

              <AppInput
                label="Mã xác thực"
                placeholder="Nhập mã 6 số"
                keyboardType="numeric"
                value={verificationCode}
                onChangeText={(value) => {
                  setVerificationCode(value);
                }}
                maxLength={6}
              />

              <AppButton
                title="Xác thực"
                loading={verifyingLoading}
                onPress={handleVerifyEmail}
                style={styles.button}
              />

              <TouchableOpacity 
                disabled={resendingCode}
                onPress={handleResendCode}
                style={styles.resendBtn}
              >
                <Text style={styles.resendText}>
                  {resendingCode ? 'Đang gửi...' : 'Gửi lại mã xác thực'}
                </Text>
              </TouchableOpacity>

              <View style={styles.footerRow}>
                <TouchableOpacity onPress={() => setIsVerifying(false)}>
                  <Text style={styles.linkText}>Quay lại Đăng ký</Text>
                </TouchableOpacity>
              </View>
            </>
          ) : (
            <>
              <Text style={styles.cardTitle}>Đăng ký</Text>

              <AppInput
                label="Họ và tên"
                placeholder="Nhập họ và tên"
                value={fullName}
                onChangeText={(value) => {
                  setFullName(value);
                  if (errors.fullName) setErrors({ ...errors, fullName: undefined });
                }}
                error={errors.fullName}
              />

              <AppInput
                label="Email"
                placeholder="Nhập email"
                keyboardType="email-address"
                autoCapitalize="none"
                value={email}
                onChangeText={(value) => {
                  setEmail(value);
                  if (errors.email) setErrors({ ...errors, email: undefined });
                }}
                error={errors.email}
              />

              <AppInput
                label="Mật khẩu"
                placeholder="Tối thiểu 6 ký tự"
                secureTextEntry
                value={password}
                onChangeText={(value) => {
                  setPassword(value);
                  if (errors.password) setErrors({ ...errors, password: undefined });
                }}
                error={errors.password}
              />

              <AppInput
                label="Xác nhận mật khẩu"
                placeholder="Nhập lại mật khẩu"
                secureTextEntry
                value={confirmPassword}
                onChangeText={(value) => {
                  setConfirmPassword(value);
                  if (errors.confirmPassword) setErrors({ ...errors, confirmPassword: undefined });
                }}
                error={errors.confirmPassword}
              />

              <AppButton
                title="Đăng ký"
                loading={loading}
                onPress={handleRegister}
                style={styles.button}
              />

              <View style={styles.footerRow}>
                <Text style={styles.footerText}>Đã có tài khoản? </Text>
                <TouchableOpacity onPress={() => navigation.navigate('Login')}>
                  <Text style={styles.linkText}>Đăng nhập</Text>
                </TouchableOpacity>
              </View>
            </>
          )}
        </AppCard>
      </ScrollView>
    </KeyboardAvoidingView>
  </PageContainer>
);
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: 'transparent',
  },
  scrollContainer: {
    flexGrow: 1,
    justifyContent: 'center',
    paddingHorizontal: 20,
    paddingVertical: 28,
  },
  headerContainer: {
    alignItems: 'center',
    marginBottom: 22,
  },
  logoMark: {
    width: 50,
    height: 50,
    borderRadius: 12,
    backgroundColor: 'rgba(76, 101, 143, 0.12)',
    borderWidth: 1,
    borderColor: COLORS.secondary,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 12,
  },
  headerTitle: {
    fontSize: 24,
    fontWeight: '800',
    color: COLORS.primaryDark,
    fontFamily: 'System',
  },
  headerSub: {
    fontSize: 14,
    color: COLORS.textMuted,
    fontFamily: 'System',
    marginTop: 6,
    textAlign: 'center',
  },
  card: {
    paddingVertical: 22,
    paddingHorizontal: 18,
  },
  cardTitle: {
    fontSize: 20,
    fontWeight: '700',
    color: COLORS.text,
    marginBottom: 18,
    fontFamily: 'System',
    textAlign: 'center',
  },
  button: {
    marginTop: 8,
  },
  footerRow: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    marginTop: 14,
  },
  footerText: {
    fontSize: 14,
    color: COLORS.textMuted,
    fontFamily: 'System',
  },
  linkText: {
    fontSize: 14,
    fontWeight: '700',
    color: COLORS.primary,
    fontFamily: 'System',
  },
  verifySub: {
    fontSize: 14,
    color: COLORS.textMuted,
    textAlign: 'center',
    marginBottom: 4,
    fontFamily: 'System',
  },
  emailText: {
    fontSize: 15,
    fontWeight: '700',
    color: COLORS.text,
    textAlign: 'center',
    marginBottom: 18,
    fontFamily: 'System',
  },
  resendBtn: {
    marginTop: 16,
    alignItems: 'center',
    padding: 8,
  },
  resendText: {
    color: COLORS.primary,
    fontWeight: '700',
    fontSize: 14,
    fontFamily: 'System',
  },
});

export default RegisterScreen;

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

const LoginScreen = ({ navigation }: any) => {
  const { login } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [errors, setErrors] = useState<{ email?: string; password?: string }>({});

  // Forgot password states
  const [isForgotPassword, setIsForgotPassword] = useState(false);
  const [forgotPasswordStep, setForgotPasswordStep] = useState<'request' | 'reset'>('request');
  const [forgotPasswordEmail, setForgotPasswordEmail] = useState('');
  const [resetCode, setResetCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmNewPassword, setConfirmNewPassword] = useState('');
  const [resetLoading, setResetLoading] = useState(false);

  // Unconfirmed email verification states
  const [isVerifyingUnconfirmed, setIsVerifyingUnconfirmed] = useState(false);
  const [unconfirmedCode, setUnconfirmedCode] = useState('');
  const [unconfirmedLoading, setUnconfirmedLoading] = useState(false);

  const validate = () => {
    let valid = true;
    const newErrors: typeof errors = {};

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

    setErrors(newErrors);
    return valid;
  };

  const handleLogin = async () => {
    if (!validate()) return;

    setLoading(true);
    try {
      await login(email, password);
    } catch (e: any) {
      if (e.message && e.message.includes('chưa được xác thực email')) {
        Alert.alert(
          'Chưa xác thực email',
          e.message,
          [
            { text: 'Hủy', style: 'cancel' },
            {
              text: 'Xác thực ngay',
              onPress: async () => {
                try {
                  await authService.requestEmailVerification(email);
                  setIsVerifyingUnconfirmed(true);
                } catch (err: any) {
                  Alert.alert('Lỗi', err.response?.data?.message || err.message || 'Không thể gửi mã xác thực.');
                }
              }
            }
          ]
        );
      } else {
        Alert.alert('Đăng nhập thất bại', e.message || 'Email hoặc mật khẩu không chính xác.');
      }
    } finally {
      setLoading(false);
    }
  };

  const handleVerifyUnconfirmedEmail = async () => {
    if (!/^\d{6}$/.test(unconfirmedCode)) {
      Alert.alert('Lỗi', 'Mã xác thực phải gồm 6 số.');
      return;
    }

    setUnconfirmedLoading(true);
    try {
      const res = await authService.verifyEmail(email, unconfirmedCode);
      if (res.success) {
        Alert.alert('Thành công', 'Email đã được xác thực thành công. Đang đăng nhập...');
        setIsVerifyingUnconfirmed(false);
        setUnconfirmedCode('');
        await login(email, password);
      } else {
        Alert.alert('Lỗi', res.message || 'Mã xác thực không hợp lệ.');
      }
    } catch (e: any) {
      Alert.alert('Lỗi', e.response?.data?.message || e.message || 'Xác thực thất bại.');
    } finally {
      setUnconfirmedLoading(false);
    }
  };

  const handleRequestReset = async () => {
    if (!forgotPasswordEmail) {
      Alert.alert('Lỗi', 'Vui lòng nhập email.');
      return;
    }
    if (!/\S+@\S+\.\S+/.test(forgotPasswordEmail)) {
      Alert.alert('Lỗi', 'Email không hợp lệ.');
      return;
    }

    setResetLoading(true);
    try {
      const res = await authService.forgotPassword(forgotPasswordEmail);
      if (res.success) {
        Alert.alert('Thành công', 'Mã xác thực đặt lại mật khẩu đã được gửi đến email của bạn.');
        setForgotPasswordStep('reset');
      } else {
        Alert.alert('Lỗi', res.message || 'Không thể gửi mã đặt lại mật khẩu.');
      }
    } catch (e: any) {
      Alert.alert('Lỗi', e.response?.data?.message || e.message || 'Không thể gửi mã đặt lại mật khẩu.');
    } finally {
      setResetLoading(false);
    }
  };

  const handleResetPassword = async () => {
    if (!/^\d{6}$/.test(resetCode)) {
      Alert.alert('Lỗi', 'Mã xác thực phải gồm 6 số.');
      return;
    }
    if (!newPassword || newPassword.length < 6) {
      Alert.alert('Lỗi', 'Mật khẩu mới phải từ 6 ký tự.');
      return;
    }
    if (newPassword !== confirmNewPassword) {
      Alert.alert('Lỗi', 'Mật khẩu xác nhận không khớp.');
      return;
    }

    setResetLoading(true);
    try {
      const res = await authService.resetPassword({
        email: forgotPasswordEmail,
        code: resetCode,
        newPassword,
        confirmNewPassword
      });
      if (res.success) {
        Alert.alert('Thành công', 'Mật khẩu đã được đặt lại thành công. Vui lòng đăng nhập với mật khẩu mới.');
        setIsForgotPassword(false);
        setEmail(forgotPasswordEmail);
        setPassword('');
        setResetCode('');
        setNewPassword('');
        setConfirmNewPassword('');
      } else {
        Alert.alert('Lỗi', res.message || 'Đặt lại mật khẩu thất bại.');
      }
    } catch (e: any) {
      Alert.alert('Lỗi', e.response?.data?.message || e.message || 'Đặt lại mật khẩu thất bại.');
    } finally {
      setResetLoading(false);
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
            <Ionicons name="checkmark-circle-outline" size={26} color={COLORS.primary} />
          </View>
          <Text style={styles.headerTitle}>Daily Expense</Text>
          <Text style={styles.headerSub}>Quản lý chi tiêu và công việc mỗi ngày</Text>
        </View>

        <AppCard style={styles.card}>
          {isVerifyingUnconfirmed ? (
            <>
              <Text style={styles.cardTitle}>Xác thực Email</Text>
              <Text style={styles.verifySub}>Mã xác thực đã được gửi đến email:</Text>
              <Text style={styles.emailText}>{email}</Text>

              <AppInput
                label="Mã xác thực"
                placeholder="Nhập mã 6 số"
                keyboardType="numeric"
                value={unconfirmedCode}
                onChangeText={setUnconfirmedCode}
                maxLength={6}
              />

              <AppButton
                title="Xác thực & Đăng nhập"
                loading={unconfirmedLoading}
                onPress={handleVerifyUnconfirmedEmail}
                style={styles.button}
              />

              <TouchableOpacity 
                style={styles.resendBtn}
                onPress={async () => {
                  try {
                    await authService.requestEmailVerification(email);
                    Alert.alert('Đã gửi lại', 'Mã xác thực mới đã được gửi về email của bạn.');
                  } catch (err: any) {
                    Alert.alert('Lỗi', err.response?.data?.message || err.message || 'Không thể gửi mã.');
                  }
                }}
              >
                <Text style={styles.resendText}>Gửi lại mã xác thực</Text>
              </TouchableOpacity>

              <View style={styles.footerRow}>
                <TouchableOpacity onPress={() => setIsVerifyingUnconfirmed(false)}>
                  <Text style={styles.linkText}>Quay lại Đăng nhập</Text>
                </TouchableOpacity>
              </View>
            </>
          ) : isForgotPassword ? (
            forgotPasswordStep === 'request' ? (
              <>
                <Text style={styles.cardTitle}>Quên Mật Khẩu</Text>
                <Text style={styles.verifySub}>Nhập email của bạn để nhận mã xác thực đặt lại mật khẩu:</Text>
                
                <AppInput
                  label="Email"
                  placeholder="Nhập email"
                  keyboardType="email-address"
                  autoCapitalize="none"
                  value={forgotPasswordEmail}
                  onChangeText={setForgotPasswordEmail}
                />

                <AppButton
                  title="Gửi mã xác thực"
                  loading={resetLoading}
                  onPress={handleRequestReset}
                  style={styles.button}
                />

                <View style={styles.footerRow}>
                  <TouchableOpacity onPress={() => setIsForgotPassword(false)}>
                    <Text style={styles.linkText}>Quay lại Đăng nhập</Text>
                  </TouchableOpacity>
                </View>
              </>
            ) : (
              <>
                <Text style={styles.cardTitle}>Đặt Lại Mật Khẩu</Text>
                <Text style={styles.verifySub}>Mã đã gửi đến: {forgotPasswordEmail}</Text>

                <AppInput
                  label="Mã xác thực"
                  placeholder="Nhập mã 6 số"
                  keyboardType="numeric"
                  value={resetCode}
                  onChangeText={setResetCode}
                  maxLength={6}
                />

                <AppInput
                  label="Mật khẩu mới"
                  placeholder="Nhập mật khẩu mới"
                  secureTextEntry
                  value={newPassword}
                  onChangeText={setNewPassword}
                />

                <AppInput
                  label="Xác nhận mật khẩu mới"
                  placeholder="Nhập lại mật khẩu mới"
                  secureTextEntry
                  value={confirmNewPassword}
                  onChangeText={setConfirmNewPassword}
                />

                <AppButton
                  title="Đặt lại mật khẩu"
                  loading={resetLoading}
                  onPress={handleResetPassword}
                  style={styles.button}
                />

                <View style={styles.footerRow}>
                  <TouchableOpacity onPress={() => setForgotPasswordStep('request')}>
                    <Text style={styles.linkText}>Quay lại nhập Email</Text>
                  </TouchableOpacity>
                </View>
              </>
            )
          ) : (
            <>
              <Text style={styles.cardTitle}>Đăng nhập</Text>

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
                placeholder="Nhập mật khẩu"
                secureTextEntry
                value={password}
                onChangeText={(value) => {
                  setPassword(value);
                  if (errors.password) setErrors({ ...errors, password: undefined });
                }}
                error={errors.password}
              />

              <TouchableOpacity 
                style={{ alignSelf: 'flex-end', marginBottom: 16, marginTop: -4 }}
                onPress={() => {
                  setIsForgotPassword(true);
                  setForgotPasswordStep('request');
                  setForgotPasswordEmail(email);
                }}
              >
                <Text style={styles.forgotPassLink}>Quên mật khẩu?</Text>
              </TouchableOpacity>

              <AppButton
                title="Đăng nhập"
                loading={loading}
                onPress={handleLogin}
                style={styles.button}
              />

              <View style={styles.footerRow}>
                <Text style={styles.footerText}>Chưa có tài khoản? </Text>
                <TouchableOpacity onPress={() => navigation.navigate('Register')}>
                  <Text style={styles.linkText}>Đăng ký</Text>
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
    marginBottom: 24,
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
  forgotPassLink: {
    fontSize: 13,
    color: COLORS.primary,
    fontWeight: '600',
    fontFamily: 'System',
  },
  verifySub: {
    fontSize: 14,
    color: COLORS.textMuted,
    textAlign: 'center',
    marginBottom: 8,
    fontFamily: 'System',
    paddingHorizontal: 10,
    lineHeight: 18,
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
export default LoginScreen;

export const getApiErrorMessage = (error: any, fallback = 'Không thể tải dữ liệu.') => {
  if (error?.code === 'ECONNABORTED') {
    return 'Máy chủ phản hồi quá chậm. Vui lòng thử lại sau vài giây.';
  }

  if (!error?.response) {
    return 'Không kết nối được máy chủ. Hãy kiểm tra mạng hoặc địa chỉ API.';
  }

  const status = error.response.status;
  const serverMessage = error.response.data?.message;

  if (serverMessage) return serverMessage;
  if (status === 401) return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
  if (status === 403) return 'Tài khoản không có quyền thực hiện thao tác này.';
  if (status === 404) return 'Không tìm thấy đường dẫn API trên máy chủ.';
  if (status >= 500) return 'Máy chủ đang lỗi hoặc chưa được cập nhật đúng bản.';

  return fallback;
};

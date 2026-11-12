export const formatMoney = (amount: number | string): string => {
  const value = typeof amount === 'string' ? Number(amount) : amount;
  if (!Number.isFinite(value)) return '0đ';

  return `${value.toLocaleString('vi-VN', {
    maximumFractionDigits: 0,
  })}đ`;
};

export const formatMoneyCompact = (amount: number | string): string => {
  return formatMoney(amount);
};

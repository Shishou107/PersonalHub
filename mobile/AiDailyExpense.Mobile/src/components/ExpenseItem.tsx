import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { Expense } from '../types/expense';
import { formatMoney } from '../utils/formatMoney';
import { formatDate } from '../utils/formatDate';
import { COLORS } from '../utils/theme';

interface ExpenseItemProps {
  item: Expense;
}

const ExpenseItem: React.FC<ExpenseItemProps> = ({ item }) => {
  const categoryColor = item.categoryColor || '#64748B';
  const isIncome = item.categoryType === 'Income';

  const getCategoryLabel = (iconName?: string) => {
    switch (iconName?.toLowerCase()) {
      case 'food': return 'Ăn';
      case 'transport': return 'Đi';
      case 'book': return 'Sách';
      case 'game': return 'Game';
      case 'home': return 'Nhà';
      case 'health': return 'Sức';
      case 'shopping': return 'Mua';
      case 'salary': return 'Lương';
      case 'income': return 'Thu';
      default: return 'Khác';
    }
  };

  return (
    <View style={styles.container}>
      <View style={[styles.iconContainer, { backgroundColor: categoryColor + '14' }]}>
        <Text style={[styles.icon, { color: categoryColor }]} numberOfLines={1} adjustsFontSizeToFit>
          {getCategoryLabel(item.categoryIcon)}
        </Text>
      </View>

      <View style={styles.textContainer}>
        <Text style={styles.title} numberOfLines={1}>{item.title}</Text>
        <View style={styles.subTextContainer}>
          <Text style={[styles.category, { color: categoryColor }]} numberOfLines={1}>
            {item.categoryName || 'Khác'}
          </Text>
          <Text style={styles.separator}>•</Text>
          <Text style={styles.date} numberOfLines={1}>{formatDate(item.spentAt)}</Text>
        </View>
      </View>

      <Text style={[styles.amount, isIncome && styles.incomeAmount]} numberOfLines={1} adjustsFontSizeToFit minimumFontScale={0.72}>
        {isIncome ? '+' : '-'}{formatMoney(item.amount)}
      </Text>
    </View>
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
  iconContainer: {
    width: 42,
    height: 34,
    borderRadius: 8,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 10,
    paddingHorizontal: 5,
  },
  icon: {
    fontSize: 11,
    fontWeight: '700',
    fontFamily: 'System',
  },
  textContainer: {
    flex: 1,
    minWidth: 0,
    marginRight: 8,
  },
  title: {
    fontSize: 15,
    fontWeight: '600',
    color: COLORS.text,
    fontFamily: 'System',
    marginBottom: 4,
  },
  subTextContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    minWidth: 0,
  },
  category: {
    maxWidth: 82,
    fontSize: 12,
    fontWeight: '500',
    fontFamily: 'System',
  },
  separator: {
    fontSize: 12,
    color: COLORS.cardBorder,
    marginHorizontal: 6,
  },
  date: {
    flex: 1,
    fontSize: 11,
    color: COLORS.textMuted,
    fontFamily: 'System',
  },
  amount: {
    width: 104,
    fontSize: 15,
    fontWeight: '700',
    color: '#DC2626',
    fontFamily: 'System',
    textAlign: 'right',
  },
  incomeAmount: {
    color: '#059669',
  },
});

export default ExpenseItem;

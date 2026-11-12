export interface Category {
  categoryId: number;
  userIđể: string;
  name: string;
  type: 'Expense' | 'Income';
  icon?: string;
  color?: string;
  isDefault: boolean;
}

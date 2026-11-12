export interface Expense {
  expenseId: number;
  userId: string;
  categoryId?: number;
  categoryName?: string;
  categoryIcon?: string;
  categoryColor?: string;
  title: string;
  amount: number;
  spentAt: string;
  paymentMethod: 'Cash' | 'Transfer';
  categoryType: 'Expense' | 'Income';
  note?: string;
  rawText?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateExpensePayload {
  title: string;
  amount: number;
  categoryId?: number;
  spentAt: string;
  paymentMethod: 'Cash' | 'Transfer';
  note?: string;
}

export interface UpdateExpensePayload {
  title: string;
  amount: number;
  categoryId: number;
  spentAt: string;
  paymentMethod: 'Cash' | 'Transfer';
  note?: string;
}

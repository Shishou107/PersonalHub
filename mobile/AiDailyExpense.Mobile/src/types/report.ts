import { Expense } from './expense';
import { Task } from './task';

export interface CategorySummary {
  categoryName: string;
  totalAmount: number;
  percent: number;
  icon?: string;
  color?: string;
}

export interface DashboardData {
  todayExpense: number;
  monthExpense: number;
  cashBalance: number;
  bankBalance: number;
  totalBalance: number;
  pendingTasks: number;
  completedTasks: number;
  overdueTasks: number;
  recentExpenses: Expense[];
  todayTasks: Task[];
}

export interface MonthlyExpenseReport {
  month: number;
  year: number;
  totalExpense: number;
  byCategory: CategorySummary[];
  aiSummary: string;
}

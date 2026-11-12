export interface Task {
  taskId: number;
  userId: string;
  title: string;
  description?: string;
  startAt?: string;
  endAt?: string;
  dueAt?: string;
  status: 'Pending' | 'Done' | 'Cancelled';
  priority: 'Low' | 'Normal' | 'High';
  repeatType: 'None' | 'Daily' | 'Weekly' | 'Monthly' | 'Yearly';
  rawText?: string;
  createdAt: string;
  updatedAt?: string;
  completedAt?: string;
  isOverdue?: boolean;
  reminderBeforeMinutes?: number;
  taskType?: 'Task' | 'Schedule';
  location?: string;
}

export interface CreateTaskPayload {
  title: string;
  description?: string;
  startAt?: string;
  endAt?: string;
  dueAt?: string;
  priority: 'Low' | 'Normal' | 'High';
  repeatType: 'None' | 'Daily' | 'Weekly' | 'Monthly' | 'Yearly';
  reminderBeforeMinutes?: number;
  taskType?: 'Task' | 'Schedule';
  location?: string;
}

export interface UpdateTaskPayload {
  title: string;
  description?: string;
  startAt?: string;
  endAt?: string;
  dueAt?: string;
  status: 'Pending' | 'Done' | 'Cancelled';
  priority: 'Low' | 'Normal' | 'High';
  repeatType: 'None' | 'Daily' | 'Weekly' | 'Monthly' | 'Yearly';
  reminderBeforeMinutes?: number;
  taskType?: 'Task' | 'Schedule';
  location?: string;
}

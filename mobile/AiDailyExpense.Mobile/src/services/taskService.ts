import api from './api';
import { ApiResponse } from '../types/api';
import { Task, CreateTaskPayload, UpdateTaskPayload } from '../types/task';

const taskService = {
  getTasks: async (date?: string, status?: string): Promise<ApiResponse<Task[]>> => {
    let url = '/tasks';
    const params = new URLSearchParams();
    if (date) params.append('date', date);
    if (status) params.append('status', status);
    const queryString = params.toString();
    if (queryString) {
      url += `?${queryString}`;
    }
    const response = await api.get<ApiResponse<Task[]>>(url);
    return response.data;
  },

  getTodayTasks: async (): Promise<ApiResponse<Task[]>> => {
    const response = await api.get<ApiResponse<Task[]>>('/tasks/today');
    return response.data;
  },

  getTaskById: async (id: number): Promise<ApiResponse<Task>> => {
    const response = await api.get<ApiResponse<Task>>(`/tasks/${id}`);
    return response.data;
  },

  createTask: async (payload: CreateTaskPayload): Promise<ApiResponse<Task>> => {
    const response = await api.post<ApiResponse<Task>>('/tasks', payload);
    return response.data;
  },

  createBatchTasks: async (payloads: CreateTaskPayload[]): Promise<ApiResponse<Task[]>> => {
    const results = await Promise.all(payloads.map((payload) => taskService.createTask(payload)));
    const createdTasks: Task[] = [];
    for (const res of results) {
      if (res.data) {
        createdTasks.push(res.data);
      }
    }
    return {
      success: true,
      message: `Đã tạo ${createdTasks.length} công việc thành công.`,
      data: createdTasks,
    };
  },

  updateTask: async (id: number, payload: UpdateTaskPayload): Promise<ApiResponse<Task>> => {
    const response = await api.put<ApiResponse<Task>>(`/tasks/${id}`, payload);
    return response.data;
  },

  deleteTask: async (id: number): Promise<ApiResponse<boolean>> => {
    const response = await api.delete<ApiResponse<boolean>>(`/tasks/${id}`);
    return response.data;
  },

  completeTask: async (id: number): Promise<ApiResponse<Task>> => {
    const response = await api.post<ApiResponse<Task>>(`/tasks/${id}/complete`);
    return response.data;
  },
};

export default taskService;

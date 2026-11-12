import React from 'react';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { Platform } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import HomeScreen from '../screens/home/HomeScreen';
import ExpenseListScreen from '../screens/expenses/ExpenseListScreen';
import AddExpenseScreen from '../screens/expenses/AddExpenseScreen';
import TaskListScreen from '../screens/tasks/TaskListScreen';
import AddTaskScreen from '../screens/tasks/AddTaskScreen';
import AiInputScreen from '../screens/ai/AiInputScreen';
import ReportScreen from '../screens/reports/ReportScreen';
import BankTransactionsScreen from '../screens/bank/BankTransactionsScreen';
import VaultScreen from '../screens/vault/VaultScreen';
import StudyHomeScreen from '../screens/study/StudyHomeScreen';
import StudyCardsScreen from '../screens/study/StudyCardsScreen';
import AddStudyCardScreen from '../screens/study/AddStudyCardScreen';
import AiVocabularyScreen from '../screens/study/AiVocabularyScreen';
import StudyReviewScreen from '../screens/study/StudyReviewScreen';
import StudyStatsScreen from '../screens/study/StudyStatsScreen';

export type ExpensesStackParamList = {
  ExpenseList: undefined;
  AddExpense: undefined;
};

export type TasksStackParamList = {
  TaskList: undefined;
  AddTask: undefined;
};

export type StudyStackParamList = {
  StudyHome: undefined;
  StudyCards: { deckId?: number; deckName?: string };
  AddStudyCard: { deckId?: number } | undefined;
  AiVocabulary: { deckId?: number } | undefined;
  StudyReview: { deckId?: number; deckName?: string } | undefined;
  StudyStats: undefined;
};

export type MainTabParamList = {
  HomeTab: undefined;
  ExpensesTab: undefined;
  TasksTab: undefined;
  StudyTab: undefined;
  AiTab: undefined;
  ReportsTab: undefined;
  BankTab: undefined;
  VaultTab: undefined;
};

const Tab = createBottomTabNavigator<MainTabParamList>();
const ExpensesStack = createNativeStackNavigator<ExpensesStackParamList>();
const TasksStack = createNativeStackNavigator<TasksStackParamList>();
const StudyStack = createNativeStackNavigator<StudyStackParamList>();

const ExpensesNavigator: React.FC = () => {
  return (
    <ExpensesStack.Navigator screenOptions={{ contentStyle: { backgroundColor: '#F8FAFC' } }}>
      <ExpensesStack.Screen
        name="ExpenseList"
        component={ExpenseListScreen}
        options={{ title: 'Chi tiêu', headerTitleStyle: { fontWeight: '700' } }}
      />
      <ExpensesStack.Screen
        name="AddExpense"
        component={AddExpenseScreen}
        options={{ title: 'Thêm chi tiêu', headerTitleStyle: { fontWeight: '700' } }}
      />
    </ExpensesStack.Navigator>
  );
};

const TasksNavigator: React.FC = () => {
  return (
    <TasksStack.Navigator screenOptions={{ contentStyle: { backgroundColor: '#F8FAFC' } }}>
      <TasksStack.Screen
        name="TaskList"
        component={TaskListScreen}
        options={{ title: 'Lịch công việc', headerTitleStyle: { fontWeight: '700' } }}
      />
      <TasksStack.Screen
        name="AddTask"
        component={AddTaskScreen}
        options={{ title: 'Thêm công việc', headerTitleStyle: { fontWeight: '700' } }}
      />
    </TasksStack.Navigator>
  );
};

const StudyNavigator: React.FC = () => {
  return (
    <StudyStack.Navigator screenOptions={{ contentStyle: { backgroundColor: '#F8FAFC' } }}>
      <StudyStack.Screen
        name="StudyHome"
        component={StudyHomeScreen}
        options={{ title: 'Học bài', headerTitleStyle: { fontWeight: '700' } }}
      />
      <StudyStack.Screen
        name="StudyCards"
        component={StudyCardsScreen}
        options={{ title: 'Danh sách thẻ', headerTitleStyle: { fontWeight: '700' } }}
      />
      <StudyStack.Screen
        name="AddStudyCard"
        component={AddStudyCardScreen}
        options={{ title: 'Thêm thẻ học', headerTitleStyle: { fontWeight: '700' } }}
      />
      <StudyStack.Screen
        name="AiVocabulary"
        component={AiVocabularyScreen}
        options={{ title: 'AI từ vựng', headerTitleStyle: { fontWeight: '700' } }}
      />
      <StudyStack.Screen
        name="StudyReview"
        component={StudyReviewScreen}
        options={{ title: 'Ôn hôm nay', headerTitleStyle: { fontWeight: '700' } }}
      />
      <StudyStack.Screen
        name="StudyStats"
        component={StudyStatsScreen}
        options={{ title: 'Thống kê học bài', headerTitleStyle: { fontWeight: '700' } }}
      />
    </StudyStack.Navigator>
  );
};

const getTabIcon = (
  routeName: keyof MainTabParamList,
  focused: boolean,
): keyof typeof Ionicons.glyphMap => {
  const icons: Record<keyof MainTabParamList, [keyof typeof Ionicons.glyphMap, keyof typeof Ionicons.glyphMap]> = {
    HomeTab: ['home-outline', 'home'],
    ExpensesTab: ['wallet-outline', 'wallet'],
    TasksTab: ['calendar-outline', 'calendar'],
    StudyTab: ['book-outline', 'book'],
    AiTab: ['sparkles-outline', 'sparkles'],
    ReportsTab: ['bar-chart-outline', 'bar-chart'],
    BankTab: ['card-outline', 'card'],
    VaultTab: ['key-outline', 'key'],
  };

  return focused ? icons[routeName][1] : icons[routeName][0];
};

const MainTabNavigator: React.FC = () => {
  return (
    <Tab.Navigator
      screenOptions={({ route }) => ({
        tabBarIcon: ({ focused, color, size }) => (
          <Ionicons
            name={getTabIcon(route.name as keyof MainTabParamList, focused)}
            size={size + 1}
            color={color}
          />
        ),
        tabBarActiveTintColor: '#4F46E5',
        tabBarInactiveTintColor: '#9CA3AF',
        tabBarLabelStyle: {
          fontSize: 10,
          fontWeight: '700',
          fontFamily: 'System',
          marginBottom: Platform.OS === 'ios' ? 0 : 4,
        },
        tabBarStyle: {
          height: Platform.OS === 'ios' ? 86 : 62,
          paddingBottom: Platform.OS === 'ios' ? 28 : 8,
          paddingTop: 8,
          backgroundColor: '#FFFFFF',
          borderTopWidth: 1,
          borderTopColor: '#E5E7EB',

        },
        headerStyle: {
          backgroundColor: '#FFFFFF',
        },
        headerTitleStyle: {
          fontWeight: '700',
          color: '#111827',
          fontFamily: 'System',
        },
      })}
    >
      <Tab.Screen
        name="HomeTab"
        component={HomeScreen}
        options={{ title: 'Tổng quan', headerTitle: 'Tổng quan' }}
      />
      <Tab.Screen
        name="ExpensesTab"
        component={ExpensesNavigator}
        options={{ title: 'Chi tiêu', headerShown: false }}
      />
      <Tab.Screen
        name="TasksTab"
        component={TasksNavigator}
        options={{ title: 'Công việc', headerShown: false }}
      />
      <Tab.Screen
        name="StudyTab"
        component={StudyNavigator}
        options={{ title: 'Học', headerShown: false }}
      />
      <Tab.Screen
        name="AiTab"
        component={AiInputScreen}
        options={{ title: 'AI', headerTitle: 'Trợ lý AI' }}
      />
      <Tab.Screen
        name="ReportsTab"
        component={ReportScreen}
        options={{ title: 'Báo cáo', headerTitle: 'Báo cáo' }}
      />
      <Tab.Screen
        name="BankTab"
        component={BankTransactionsScreen}
        options={{ title: 'Ngân hàng', headerTitle: 'Ngân hàng' }}
      />
      <Tab.Screen
        name="VaultTab"
        component={VaultScreen}
        options={{ title: 'TK', headerTitle: 'Kho Tài Khoản' }}
      />
    </Tab.Navigator>
  );
};

export default MainTabNavigator;

import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import AppCard from './AppCard';

interface SummaryCardProps {
  title: string;
  value: string;
  subText?: string;
  color?: string;
  icon?: string;
}

const SummaryCard: React.FC<SummaryCardProps> = ({ title, value, subText, color = '#2563EB', icon }) => {
  return (
    <AppCard style={styles.card}>
      <View style={styles.row}>
        <View style={styles.textContainer}>
          <Text style={styles.title} numberOfLines={2}>{title}</Text>
          <Text
            style={[styles.value, { color }]}
            numberOfLines={1}
            adjustsFontSizeToFit
            minimumFontScale={0.72}
          >
            {value}
          </Text>
          {subText ? <Text style={styles.subText} numberOfLines={1}>{subText}</Text> : null}
        </View>
        {icon ? (
          <View style={[styles.iconContainer, { backgroundColor: color + '12' }]}>
            <Text style={styles.icon}>{icon}</Text>
          </View>
        ) : null}
      </View>
    </AppCard>
  );
};

const styles = StyleSheet.create({
  card: {
    flex: 1,
    marginHorizontal: 4,
    padding: 12,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  textContainer: {
    flex: 1,
    minWidth: 0,
  },
  title: {
    fontSize: 12,
    fontWeight: '600',
    color: '#64748B',
    marginBottom: 6,
    fontFamily: 'System',
    lineHeight: 15,
  },
  value: {
    fontSize: 20,
    fontWeight: '800',
    fontFamily: 'System',
    marginBottom: 2,
  },
  subText: {
    fontSize: 11,
    color: '#94A3B8',
    fontFamily: 'System',
  },
  iconContainer: {
    width: 34,
    height: 34,
    borderRadius: 8,
    justifyContent: 'center',
    alignItems: 'center',
    marginLeft: 8,
  },
  icon: {
    fontSize: 18,
  },
});

export default SummaryCard;

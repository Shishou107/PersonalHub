import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import type { AiVocabularyAnalyzeResponseData } from '../services/aiService';
import { COLORS } from '../utils/theme';

interface StudyExtendedAnalysisProps {
  extendedData?: string;
}

export const parseExtendedAnalysis = (value?: string): AiVocabularyAnalyzeResponseData | null => {
  if (!value) return null;

  try {
    const data: any = JSON.parse(value);
    const family = data.wordFamily || data.word_family || {};
    const forms = data.forms || {};
    return {
      language: data.language || '',
      input: data.input || '',
      meaningVi: data.meaningVi || data.meaning_vi || '',
      baseWord: data.baseWord || data.base_word || '',
      wordFamily: {
        noun: family.noun || [],
        verb: family.verb || [],
        adjective: family.adjective || [],
        adverb: family.adverb || [],
      },
      forms: {
        plural: forms.plural ? String(forms.plural).trim() : undefined,
        v2: forms.v2 ? String(forms.v2).trim() : undefined,
        v3: forms.v3 ? String(forms.v3).trim() : undefined,
        v_ing: (forms.v_ing || forms.vIng) ? String(forms.v_ing || forms.vIng).trim() : undefined,
        v_s_es: (forms.v_s_es || forms.vSes) ? String(forms.v_s_es || forms.vSes).trim() : undefined,
        comparative: forms.comparative ? String(forms.comparative).trim() : undefined,
        superlative: forms.superlative ? String(forms.superlative).trim() : undefined,
      },
      synonyms: (data.synonyms || []).map((item: any) => ({
        word: item.word || '',
        reading: item.reading || '',
        meaningVi: item.meaningVi || item.meaning_vi || '',
        note: item.note || '',
      })),
    };
  } catch {
    return null;
  }
};

const StudyExtendedAnalysis: React.FC<StudyExtendedAnalysisProps> = ({ extendedData }) => {
  const analysis = parseExtendedAnalysis(extendedData);
  if (!analysis) return null;

  const familyRows = [
    { label: 'Danh từ (Noun)', values: analysis.wordFamily?.noun || [] },
    { label: 'Động từ (Verb)', values: analysis.wordFamily?.verb || [] },
    { label: 'Tính từ (Adjective)', values: analysis.wordFamily?.adjective || [] },
  ].filter((row) => row.values.length > 0);

  const formRows = [
    { label: 'V2 (Quá khứ)', value: analysis.forms?.v2 },
    { label: 'V3 (Phân từ II)', value: analysis.forms?.v3 },
    { label: 'V-ing', value: analysis.forms?.v_ing },
    { label: 'V(s/es)', value: analysis.forms?.v_s_es },
    { label: 'Số nhiều (Plural)', value: analysis.forms?.plural },
    { label: 'So sánh hơn', value: analysis.forms?.comparative },
    { label: 'So sánh nhất', value: analysis.forms?.superlative },
  ].filter((row): row is { label: string; value: string } => Boolean(row.value));

  const hasData =
    analysis.meaningVi ||
    analysis.baseWord ||
    familyRows.length > 0 ||
    formRows.length > 0 ||
    (analysis.synonyms && analysis.synonyms.length > 0);

  if (!hasData) return null;

  return (
    <View style={styles.analysisCard}>
      <Text style={styles.analysisHeader}>Phân tích mở rộng</Text>
      {!!analysis.baseWord && <Text style={styles.analysisSummary}>Từ gốc: {analysis.baseWord}</Text>}
      {!!analysis.meaningVi && <Text style={styles.analysisSummary}>Nghĩa: {analysis.meaningVi}</Text>}

      {formRows.length > 0 ? (
        <View style={styles.analysisBlock}>
          <Text style={styles.analysisTitle}>Dạng biến thể (Word Forms)</Text>
          {formRows.map((row) => (
            <View key={row.label} style={styles.tableRow}>
              <Text style={styles.tableLabel}>{row.label}</Text>
              <Text style={styles.tableValue}>{row.value}</Text>
            </View>
          ))}
        </View>
      ) : null}

      {analysis.language === 'english' && familyRows.length > 0 ? (
        <View style={styles.analysisBlock}>
          <Text style={styles.analysisTitle}>Word Family (Danh từ - Động từ - Tính từ)</Text>
          {familyRows.map((row) => (
            <View key={row.label} style={styles.tableRow}>
              <Text style={styles.tableLabel}>{row.label}</Text>
              <Text style={styles.tableValue}>{row.values.join(', ')}</Text>
            </View>
          ))}
        </View>
      ) : null}

      {analysis.synonyms && analysis.synonyms.length > 0 ? (
        <View style={styles.analysisBlock}>
          <Text style={styles.analysisTitle}>Từ đồng nghĩa</Text>
          {analysis.synonyms.map((item, itemIndex) => (
            <View key={`${item.word}-${itemIndex}`} style={styles.synonymRow}>
              <Text style={styles.synonymWord}>{item.word || '-'}</Text>
              {!!item.reading && <Text style={styles.synonymReading}>{item.reading}</Text>}
              <Text style={styles.synonymMeaning}>{item.meaningVi || '-'}</Text>
              {!!item.note && <Text style={styles.synonymNote}>{item.note}</Text>}
            </View>
          ))}
        </View>
      ) : null}
    </View>
  );
};

const styles = StyleSheet.create({
  analysisCard: {
    marginTop: 14,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: COLORS.cardBorder,
    backgroundColor: 'rgba(255, 255, 255, 0.56)',
    padding: 10,
  },
  analysisHeader: {
    color: COLORS.text,
    fontSize: 14,
    fontWeight: '900',
    marginBottom: 6,
  },
  analysisSummary: {
    color: COLORS.text,
    fontSize: 12,
    fontWeight: '700',
    marginTop: 3,
  },
  analysisBlock: {
    marginTop: 8,
  },
  analysisTitle: {
    color: COLORS.primary,
    fontSize: 12,
    fontWeight: '900',
    marginBottom: 5,
  },
  tableRow: {
    flexDirection: 'row',
    borderTopWidth: 1,
    borderTopColor: COLORS.cardBorder,
    paddingVertical: 6,
    gap: 8,
  },
  tableLabel: {
    width: 110,
    color: COLORS.text,
    fontSize: 12,
    fontWeight: '900',
  },
  tableValue: {
    flex: 1,
    color: COLORS.text,
    fontSize: 12,
    fontWeight: '700',
  },
  synonymRow: {
    borderTopWidth: 1,
    borderTopColor: COLORS.cardBorder,
    paddingVertical: 7,
  },
  synonymWord: {
    color: COLORS.text,
    fontSize: 12,
    fontWeight: '900',
  },
  synonymReading: {
    color: COLORS.secondary,
    fontSize: 11,
    fontWeight: '800',
    marginTop: 2,
  },
  synonymMeaning: {
    color: COLORS.text,
    fontSize: 12,
    fontWeight: '700',
    marginTop: 2,
  },
  synonymNote: {
    color: COLORS.textMuted,
    fontSize: 11,
    fontWeight: '600',
    marginTop: 2,
  },
});

export default StudyExtendedAnalysis;

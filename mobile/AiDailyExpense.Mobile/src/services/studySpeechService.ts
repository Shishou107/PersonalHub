import { NativeModules, Platform } from 'react-native';

const { StudySpeech } = NativeModules;

const studySpeechService = {
  speak: async (text: string, language?: string): Promise<boolean> => {
    if (Platform.OS !== 'android' || !StudySpeech?.speak) {
      return false;
    }

    try {
      return await StudySpeech.speak(text, language || '');
    } catch {
      return false;
    }
  },

  stop: async (): Promise<boolean> => {
    if (Platform.OS !== 'android' || !StudySpeech?.stop) {
      return false;
    }

    try {
      return await StudySpeech.stop();
    } catch {
      return false;
    }
  },
};

export default studySpeechService;

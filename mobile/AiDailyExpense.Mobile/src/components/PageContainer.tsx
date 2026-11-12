import React from 'react';
import { StyleSheet, ImageBackground, View, ViewStyle, StatusBar } from 'react-native';

interface PageContainerProps {
  children: React.ReactNode;
  style?: ViewStyle;
}

const PageContainer: React.FC<PageContainerProps> = ({ children, style }) => {
  return (
    <ImageBackground
      source={require('../../assets/nenchoapp.jpg')}
      style={styles.background}
      resizeMode="cover"
    >
      <StatusBar barStyle="dark-content" backgroundColor="transparent" translucent />
      <View style={[styles.container, style]}>
        {children}
      </View>
    </ImageBackground>
  );
};

const styles = StyleSheet.create({
  background: {
    flex: 1,
    width: '100%',
    maxWidth: 500,
    alignSelf: "center",
    height: '100%',
  },
  container: {
    flex: 1,
  },
});

export default PageContainer;

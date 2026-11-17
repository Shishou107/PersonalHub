const expo = require("eslint-config-expo/flat");

module.exports = [
  ...expo,
  {
    ignores: ["node_modules/**", ".expo/**", "android/**", "dist/**", "web-build/**", ".npm-cache/**"],
  },
  {
    files: ["**/*.ts", "**/*.tsx"],
    rules: {
      "no-unused-vars": "off",
      "@typescript-eslint/no-unused-vars": "warn",
      "@typescript-eslint/array-type": "warn",
      "react-hooks/rules-of-hooks": "error",
      "react-hooks/exhaustive-deps": "warn",
      "react-hooks/set-state-in-effect": "warn",
      "react-hooks/immutability": "warn",
      "react/no-unescaped-entities": "warn",
    },
  },
];

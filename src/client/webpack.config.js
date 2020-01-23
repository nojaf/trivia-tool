const path = require("path");
const webpack = require("webpack");
const MinifyPlugin = require("terser-webpack-plugin");
const CopyWebpackPlugin = require("copy-webpack-plugin");
const HtmlWebpackPlugin = require("html-webpack-plugin");
const MiniCssExtractPlugin = require('mini-css-extract-plugin');

function resolve(filePath) {
  return path.join(__dirname, filePath);
}

const CONFIG = {
  fsharpEntry: {
    app: [resolve("./client.fsproj")]
  },
  devServerProxy: {
    "/api/*": {
      target: "http://localhost:7071",
      changeOrigin: true
    }
  },
  historyApiFallback: {
    index: resolve("./index.html")
  },
  contentBase: resolve("./public"),
  // Use babel-preset-env to generate JS compatible with most-used browsers.
  // More info at https://github.com/babel/babel/blob/master/packages/babel-preset-env/README.md
  babel: {
    presets: [
      [
        "@babel/preset-env",
        {
          targets: {
            browsers: ["last 2 versions"]
          },
          modules: false
        }
      ],
      "@babel/preset-react"
    ],
    plugins: ["@babel/plugin-proposal-class-properties"]
  }
};

const isProduction = process.argv.indexOf("-p") >= 0;
console.log(
  "Bundling for " + (isProduction ? "production" : "development") + "..."
);

const commonPlugins = [
  new MiniCssExtractPlugin({
    filename: isProduction ? "[name].[hash].css" : "[name].css",
    chunkFilename: isProduction ? "[name].[hash].css" : "[name].css",
  }),
  new HtmlWebpackPlugin({
    filename: resolve("./output/index.html"),
    template: resolve("./public/index.html")
  })
];

module.exports = {
  entry: CONFIG.fsharpEntry,
  output: {
    path: resolve("./output"),
    filename: isProduction ? "[name].[hash].js" : "[name].js",
    publicPath: isProduction ? "/trivia-tool/" : "/"
  },
  mode: isProduction ? "production" : "development",
  devtool: isProduction ? undefined : "source-map",
  optimization: {
    // Split the code coming from npm packages into a different file.
    // 3rd party dependencies change less often, let the browser cache them.
    splitChunks: {
      cacheGroups: {
        commons: {
          test: /node_modules/,
          name: "vendors",
          chunks: "all"
        }
      }
    },
    minimizer: isProduction ? [new MinifyPlugin()] : []
  },
  // DEVELOPMENT
  //      - HotModuleReplacementPlugin: Enables hot reloading when code changes without refreshing
  plugins: isProduction
    ? commonPlugins.concat([
        new CopyWebpackPlugin([{ from: resolve("./public") }]),
        // ensure that we get a production build of any dependencies
        // this is primarily for React, where this removes 179KB from the bundle
        new webpack.DefinePlugin({
          "process.env.NODE_ENV": '"production"'
        })
      ])
    : commonPlugins.concat([new webpack.HotModuleReplacementPlugin()]),
  // Configuration for webpack-dev-server
  devServer: {
    proxy: CONFIG.devServerProxy,
    hot: true,
    inline: true,
    historyApiFallback: CONFIG.historyApiFallback,
    contentBase: CONFIG.contentBase
  },
  // - fable-loader: transforms F# into JS
  // - babel-loader: transforms JS to old syntax (compatible with old browsers)
  module: {
    rules: [
      {
        test: /\.fs(x|proj)?$/,
        use: "fable-loader"
      },
      {
        test: /\.js$/,
        exclude: /node_modules/,
        use: {
          loader: "babel-loader",
          options: CONFIG.babel
        }
      },
      {
        test: /\.css$/i,
        use: [MiniCssExtractPlugin.loader, 'css-loader'],
      },
      {
        test: /\.(png|jpg|jpeg|gif|svg|woff|woff2|ttf|eot)(\?.*$|$)/,
        use: ["file-loader"]
      }
    ]
  }
};

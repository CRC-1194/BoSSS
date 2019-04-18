const path = require('path');
const webpack = require('webpack');

module.exports = {
    mode: 'development',
	entry: {
		"app": './src/boSSSpad/index.js',
		"editor.worker": 'monaco-editor/esm/vs/editor/editor.worker.js',
		"json.worker": 'monaco-editor/esm/vs/language/json/json.worker',
		"css.worker": 'monaco-editor/esm/vs/language/css/css.worker',
		"html.worker": 'monaco-editor/esm/vs/language/html/html.worker',
		"ts.worker": 'monaco-editor/esm/vs/language/typescript/ts.worker',
	},
	output: {
		filename: '[name].bundle.js',
		path: path.resolve(__dirname, 'boSSSpad')
	},
	module: {
		rules: [{
			test: /\.css$/,
			use: [ 'style-loader', 'css-loader' ]
		}]
	},
	plugins: [
		new webpack.IgnorePlugin(/^((fs)|(path)|(os)|(crypto)|(source-map-support))$/, /vs\/language\/typescript\/lib/)
	],
	target: 'electron-main',
	node: {
		__dirname : false
	},
	externals: {
		'electron-edge-js': 'require("electron-edge-js")'
	}
}
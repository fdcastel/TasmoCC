import React from 'react';
import { Route } from 'react-router';
import { Layout } from './components/layout/Layout';
import { Home } from './containers/Home';

const App = () => (
    <Layout>
        <Route exact path="/" component={Home} />
    </Layout>
);

App.displayName = App.name;

export default App;

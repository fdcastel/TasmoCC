import React from 'react';
import { Route } from 'react-router';
import { Layout } from './components/layout/Layout';
import { Home } from './containers/Home';
import { Devices } from './containers/Devices';

const App = () => (
    <Layout>
        <Route exact path="/" component={Home} />
        <Route path="/devices" component={Devices} />
    </Layout>
);

App.displayName = App.name;

export default App;

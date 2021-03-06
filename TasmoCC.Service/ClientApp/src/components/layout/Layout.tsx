import React from 'react';
import { NavMenu } from './NavMenu';

export const Layout = (props: any) => (
    <div>
        <NavMenu />
        <div className="container-fluid">{props.children}</div>
    </div>
);

Layout.displayName = Layout.name;
